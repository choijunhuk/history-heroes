package com.choijunhuk.historyheroes.kakao;

import android.app.Activity;
import android.content.Context;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.util.Base64;
import android.util.Log;

import com.kakao.sdk.auth.AuthApiClient;
import com.kakao.sdk.auth.model.OAuthToken;
import com.kakao.sdk.common.KakaoSdk;
import com.kakao.sdk.common.model.ClientError;
import com.kakao.sdk.common.model.ClientErrorCause;
import com.kakao.sdk.user.UserApiClient;

import org.json.JSONObject;

import java.nio.charset.StandardCharsets;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import kotlin.Unit;
import kotlin.jvm.functions.Function2;

/**
 * 카카오 로그인 다리: 카카오 SDK로 로그인해서 OpenID Connect id_token을 C#(KakaoLogin.cs)에 넘긴다.
 * 결과는 언제나 Callback.onResult 한 번 (자바 스레드에서 온다 → C#은 TaskCompletionSource만 채운다).
 */
public final class KakaoBridge {
    /** status: "ok" | "cancelled" | "error". ok면 idToken, error면 code (화면에 보일 짧은 원인) */
    public interface Callback {
        void onResult(String status, String idToken, String code);
    }

    private static final String TAG = "KakaoBridge";
    /** 빌드 때 AndroidManifestPatch가 넣는 메타데이터 (키는 저장소 밖 ~/.history-heroes/kakao.properties) */
    private static final String KEY_META = "com.choijunhuk.historyheroes.KAKAO_APP_KEY";
    private static final Pattern KOE = Pattern.compile("KOE\\d+");

    private KakaoBridge() {
    }

    /** 빌드에 들어간 네이티브 앱 키. 없으면 null (그 빌드는 카카오 로그인을 숨긴다) */
    public static String appKey(Context context) {
        try {
            ApplicationInfo info = context.getPackageManager().getApplicationInfo(context.getPackageName(), PackageManager.GET_META_DATA);
            Bundle meta = info.metaData;
            Object value = meta == null ? null : meta.get(KEY_META);
            return value == null || value.toString().isEmpty() ? null : value.toString();
        } catch (Exception e) {
            return null;
        }
    }

    private static boolean init(Context context) {
        String key = appKey(context);
        if (key == null) return false;
        if (!KakaoSdk.INSTANCE.isInitialized()) KakaoSdk.init(context.getApplicationContext(), key);
        return true;
    }

    /** 로그인 → id_token */
    public static void login(final Activity activity, final Callback cb) {
        activity.runOnUiThread(() -> {
            try {
                if (!init(activity)) {
                    cb.onResult("error", null, "NoAppKey");
                    return;
                }
                startLogin(activity, (token, error) -> {
                    deliver(activity, cb, token, error);
                    return Unit.INSTANCE;
                });
            } catch (Throwable t) {
                Log.e(TAG, "login failed", t);
                cb.onResult("error", null, t.getClass().getSimpleName());
            }
        });
    }

    /**
     * 이 기기에 남은 카카오 로그인(토큰)의 회원번호. 토큰이 없거나 만료면 error "NoToken" 등.
     * 계정 삭제 때 다시 로그인하지 않고 끊어도 되는지(같은 카카오 계정인지) C#이 비교한다.
     */
    public static void storedUser(final Activity activity, final Callback cb) {
        activity.runOnUiThread(() -> {
            try {
                if (!init(activity)) {
                    cb.onResult("error", null, "NoAppKey");
                    return;
                }
                if (!AuthApiClient.getInstance().hasToken()) {
                    cb.onResult("error", null, "NoToken");
                    return;
                }
                UserApiClient.getInstance().me((user, error) -> {
                    if (error != null || user == null || user.getId() == null) {
                        cb.onResult("error", null, error == null ? "NoUser" : code(error));
                    } else {
                        cb.onResult("ok", String.valueOf(user.getId()), null);
                    }
                    return Unit.INSTANCE;
                });
            } catch (Throwable t) {
                Log.e(TAG, "storedUser failed", t);
                cb.onResult("error", null, t.getClass().getSimpleName());
            }
        });
    }

    /** 지금 카카오 로그인(토큰)으로 연결 끊기. 로그인은 하지 않는다 — 어느 계정인지는 C#이 먼저 확인한다 */
    public static void unlink(final Activity activity, final Callback cb) {
        activity.runOnUiThread(() -> {
            try {
                if (!init(activity)) {
                    cb.onResult("error", null, "NoAppKey");
                    return;
                }
                UserApiClient.getInstance().unlink(e -> {
                    if (e != null) Log.w(TAG, "unlink failed", e);
                    cb.onResult(e == null ? "ok" : "error", null, e == null ? null : code(e));
                    return Unit.INSTANCE;
                });
            } catch (Throwable t) {
                Log.e(TAG, "unlink failed", t);
                cb.onResult("error", null, t.getClass().getSimpleName());
            }
        });
    }

    /** 이 기기의 카카오 로그인(토큰)만 지운다 (다른 카카오 계정으로 로그인했을 때 그 계정을 건드리지 않게) */
    public static void logout(final Activity activity, final Callback cb) {
        activity.runOnUiThread(() -> {
            try {
                if (!init(activity)) {
                    cb.onResult("error", null, "NoAppKey");
                    return;
                }
                UserApiClient.getInstance().logout(e -> {
                    cb.onResult(e == null ? "ok" : "error", null, e == null ? null : code(e));
                    return Unit.INSTANCE;
                });
            } catch (Throwable t) {
                cb.onResult("error", null, t.getClass().getSimpleName());
            }
        });
    }

    /** 카카오톡이 있으면 카카오톡으로, 안 되면 카카오 계정(브라우저)으로. 사용자가 취소했으면 다른 방법으로 넘어가지 않는다 (카카오 공식 예제) */
    private static void startLogin(final Activity activity, final Function2<OAuthToken, Throwable, Unit> done) {
        final UserApiClient client = UserApiClient.getInstance();
        if (client.isKakaoTalkLoginAvailable(activity)) {
            client.loginWithKakaoTalk(activity, (token, error) -> {
                if (error != null && !isCancel(error)) client.loginWithKakaoAccount(activity, done);
                else done.invoke(token, error);
                return Unit.INSTANCE;
            });
        } else {
            client.loginWithKakaoAccount(activity, done);
        }
    }

    private static void deliver(Context context, Callback cb, OAuthToken token, Throwable error) {
        if (error != null) {
            Log.w(TAG, "login failed", error);
            cb.onResult(isCancel(error) ? "cancelled" : "error", null, code(error));
            return;
        }
        String idToken = token == null ? null : token.getIdToken();
        if (idToken == null) {
            // 카카오 디벨로퍼스에서 OpenID Connect가 꺼져 있으면 id_token이 오지 않는다
            cb.onResult("error", null, "NoIdToken");
            return;
        }
        Log.i(TAG, describe(idToken, appKey(context)));
        cb.onResult("ok", idToken, null);
    }

    private static boolean isCancel(Throwable e) {
        return e instanceof ClientError && ((ClientError) e).getReason() == ClientErrorCause.Cancelled;
    }

    /** 짧은 원인: "KOE009"(키 해시 미등록) 같은 카카오 오류 번호, 없으면 "AuthError.Misconfigured" 같은 종류.원인 */
    private static String code(Throwable e) {
        Matcher m = KOE.matcher(String.valueOf(e.getMessage()));
        if (m.find()) return m.group();
        String c = e.getClass().getSimpleName();
        try {
            Object reason = e.getClass().getMethod("getReason").invoke(e);
            if (reason != null) c += "." + reason;
        } catch (Exception ignored) {
            // 원인이 없는 오류 종류
        }
        return c;
    }

    /** id_token 진단 한 줄 (logcat). aud 값은 남기지 않고 앱 키와 같은지만 적는다 */
    private static String describe(String jwt, String key) {
        try {
            String[] parts = jwt.split("\\.");
            String json = new String(Base64.decode(parts[1], Base64.URL_SAFE | Base64.NO_WRAP | Base64.NO_PADDING), StandardCharsets.UTF_8);
            JSONObject payload = new JSONObject(json);
            String aud = payload.optString("aud");
            return "id_token iss=" + payload.optString("iss")
                    + " aud=" + (aud.equals(key) ? "native-app-key" : "OTHER(not the native app key)")
                    + " exp=" + payload.optLong("exp");
        } catch (Exception e) {
            return "id_token unreadable: " + e.getClass().getSimpleName();
        }
    }
}
