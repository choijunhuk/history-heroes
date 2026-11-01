using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;

namespace HistoryHeroes.Account
{
    /// <summary>
    /// 외부 로그인 하나 (지금은 카카오, 다음은 구글 플레이 게임즈). AccountService는 이 모양만 보고 연결 · 계정 전환 · 삭제를 한다 →
    /// 구글은 같은 모양으로 하나 더 만들어 AccountService.Providers에 넣으면 된다 (UGS: LinkWithGooglePlayGamesAsync(authCode) 등).
    /// </summary>
    public sealed class LoginProvider
    {
        public enum Status
        {
            Ok,
            Cancelled,
            Error,
        }

        /// <summary>폰 SDK 로그인 결과. Ok면 Token(카카오: OIDC id_token, 구글: 서버 인증 코드), Error면 Code(짧은 원인)</summary>
        public readonly struct Result
        {
            public readonly Status Status;
            public readonly string Token;
            public readonly string Code;

            public Result(Status status, string token, string code)
            {
                Status = status;
                Token = token;
                Code = code;
            }
        }

        /// <summary>화면에 쓰는 이름 ("카카오")</summary>
        public string DisplayName;
        /// <summary>이 기기에서 쓸 수 있나 (안드로이드 + 앱 키가 빌드에 들어감)</summary>
        public Func<bool> Available;
        /// <summary>폰 SDK 로그인 (화면이 SDK로 넘어갔다 돌아온다)</summary>
        public Func<Task<Result>> Login;
        /// <summary>지금 UGS 플레이어에 붙이기</summary>
        public Func<string, Task> Link;
        /// <summary>이 외부 계정이 붙은 UGS 플레이어로 로그인 (계정 전환)</summary>
        public Func<string, Task> SignIn;
        /// <summary>UGS 플레이어에 붙은 이 외부 계정의 ID (없으면 null)</summary>
        public Func<PlayerInfo, string> LinkedId;
        /// <summary>
        /// 계정 삭제 때 외부 쪽 연결 끊기. 인자 = LinkedId (다른 외부 계정을 끊지 않도록 확인용).
        /// Ok가 아니면 AccountService가 "연결 끊기를 건너뛰고 삭제"를 묻는다.
        /// </summary>
        public Func<string, Task<Result>> Unlink;

        public bool IsLinked(PlayerInfo info) => !string.IsNullOrEmpty(LinkedId(info));
    }
}
