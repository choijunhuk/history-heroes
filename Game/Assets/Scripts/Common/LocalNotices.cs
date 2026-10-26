using System;
using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 로컬 알림 (LiveOps 9장, 서버 없이 기기에서 예약): "하트가 다 찼어요" · "순찰 보상이 가득 찼어요".
    /// 21:00~08:00에는 보내지 않는다 (걸리면 다음 08:00으로). 종류마다 하나라서 하루 최대 2번. 설정에서 종류별로 끈다.
    /// "손해" 같은 겁주는 말은 쓰지 않는다. 앱이 뒤로 갈 때마다 전부 지우고 다시 예약한다.
    /// </summary>
    public static class LocalNotices
    {
        public const string Channel = "reminders";
        public const int HeartsId = 1001;
        public const int PatrolId = 1002;
        public const int QuietStart = 21;
        public const int QuietEnd = 8;

        public struct Planned
        {
            public int Id;
            public string Title;
            public string Text;
            public DateTimeOffset At;
        }

        /// <summary>조용한 시간(21:00~08:00)이면 다음 08:00으로 미룬다</summary>
        public static DateTimeOffset OutOfQuiet(DateTimeOffset t)
        {
            if (t.Hour >= QuietStart) return new DateTimeOffset(t.Date.AddDays(1).AddHours(QuietEnd), t.Offset);
            if (t.Hour < QuietEnd) return new DateTimeOffset(t.Date.AddHours(QuietEnd), t.Offset);
            return t;
        }

        /// <summary>지금 저장 상태로 보낼 알림 (기기 시각 기준)</summary>
        public static List<Planned> Plan(SaveData s, DateTimeOffset now)
        {
            var list = new List<Planned>();
            long unix = now.ToUnixTimeSeconds();
            GameSettings settings = s.settings;

            if (settings.notifyHearts)
            {
                int next = Hearts.SecondsToNext(s, unix);
                if (s.hearts < Hearts.Max)
                {
                    long full = next + (long)(Hearts.Max - s.hearts - 1) * Hearts.RegenSeconds;
                    list.Add(new Planned { Id = HeartsId, Title = "하트가 다 찼어요", Text = "던전에 한 판 들어가 볼까요?", At = OutOfQuiet(now.AddSeconds(full)) });
                }
            }

            if (settings.notifyPatrol)
            {
                long fullAt = Patrol.FullAt(s);
                if (fullAt > unix)
                {
                    DateTimeOffset at = DateTimeOffset.FromUnixTimeSeconds(fullAt).ToOffset(now.Offset);
                    list.Add(new Planned { Id = PatrolId, Title = "순찰 보상이 가득 찼어요", Text = "로비에서 골드를 받아 가세요", At = OutOfQuiet(at) });
                }
            }

            return list;
        }

        /// <summary>예약을 새로 한다 (앱이 뒤로 갈 때 · 꺼질 때). 안드로이드 기기에서만</summary>
        public static void Reschedule(SaveData s)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidNotificationCenter.CancelAllScheduledNotifications();
                if (!s.settings.notifyAsked || AndroidNotificationCenter.UserPermissionToPost != PermissionStatus.Allowed) return;
                AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(Channel, "알림", "하트·순찰이 가득 찼을 때", Importance.Default));
                foreach (Planned p in Plan(s, Clock.Now()))
                {
                    var n = new AndroidNotification(p.Title, p.Text, p.At.LocalDateTime);
                    AndroidNotificationCenter.SendNotificationWithExplicitID(n, Channel, p.Id);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[LocalNotices] 예약 실패: " + e.Message);
            }
#endif
        }

        /// <summary>알림 권한 묻기 (안드로이드 13부터 OS 창). 첫 판이 끝난 뒤 이유를 보여 준 다음에 부른다</summary>
        public static void RequestPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (AndroidNotificationCenter.UserPermissionToPost != PermissionStatus.Allowed) new PermissionRequest();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[LocalNotices] 권한 요청 실패: " + e.Message);
            }
#endif
        }
    }
}
