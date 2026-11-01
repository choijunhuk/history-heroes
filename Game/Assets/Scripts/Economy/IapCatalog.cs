#nullable disable
using System.Collections.Generic;

namespace HistoryHeroes.Economy
{
    /// <summary>
    /// 실제 결제 상품표 (Monetization 3-1 · 3-2). 플레이 콘솔에 만들 상품과 똑같이 둔다 — Docs/Release/GooglePlay-Checklist.md 3장이 이 표를 옮겨 적은 것.
    /// 서버 결제 검증 모듈(Server/CloudCode)도 이 파일을 그대로 컴파일해 지급 수량을 정한다 → UnityEngine을 쓰지 말 것. 고치면 모듈을 다시 배포한다.
    /// 상품 ID는 플레이 콘솔에 한 번 만들면 바꾸거나 다시 쓸 수 없다 → 절대 고치지 않는다 (없앨 때도 ID는 비워 둔다).
    /// 화면의 가격은 언제나 상점이 준 글(localizedPriceString)을 쓴다. 여기 원화 값은 콘솔 등록 · 문서용이다.
    /// 지급 수량이 플레이 콘솔 설명과 이어져 있어 에셋(밸런스 값)이 아니라 코드 상수로 둔다.
    /// </summary>
    public static class IapCatalog
    {
        public const string StarterPack = "starter_pack";

        public sealed class Item
        {
            public readonly string Id;
            /// <summary>상점 카드 이름 = 플레이 콘솔 상품 이름</summary>
            public readonly string Title;
            /// <summary>플레이 콘솔 상품 설명 (200자 이내)</summary>
            public readonly string Description;
            public readonly int PriceKrw;
            /// <summary>결제로 산 기본 보석 (유료분)</summary>
            public readonly int PaidGems;
            /// <summary>보너스 보석 (무료분). 첫 구매 2배와 따로다</summary>
            public readonly int FreeGems;
            public readonly int Tickets;
            public readonly int Gold;
            /// <summary>계정당 1번만 지급 (첫 결제 패키지). 보석 상품은 대신 첫 구매 2배가 있다</summary>
            public readonly bool OncePerAccount;

            public bool FirstDouble => !OncePerAccount;

            public Item(string id, string title, string description, int priceKrw, int paidGems, int freeGems, int tickets = 0, int gold = 0, bool oncePerAccount = false)
            {
                Id = id;
                Title = title;
                Description = description;
                PriceKrw = priceKrw;
                PaidGems = paidGems;
                FreeGems = freeGems;
                Tickets = tickets;
                Gold = gold;
                OncePerAccount = oncePerAccount;
            }
        }

        /// <summary>상점에 놓는 순서 (첫 결제 패키지가 맨 앞)</summary>
        public static readonly IReadOnlyList<Item> All = new[]
        {
            new Item(StarterPack, "첫 결제 패키지", "보석 300개, 고급 소환권 2장, 골드 10,000을 한 번에 받아요. 계정당 1번만 살 수 있어요.",
                1200, 100, 200, tickets: 2, gold: 10000, oncePerAccount: true),
            new Item("gems_t1", "보석 한 줌", "보석 100개. 상품마다 처음 한 번은 기본 보석만큼 더 받아요.", 1200, 100, 0),
            new Item("gems_t2", "보석 주머니", "보석 500개 + 보너스 50개. 상품마다 처음 한 번은 기본 보석만큼 더 받아요.", 5900, 500, 50),
            new Item("gems_t3", "보석 상자", "보석 1,000개 + 보너스 150개. 상품마다 처음 한 번은 기본 보석만큼 더 받아요.", 12000, 1000, 150),
            new Item("gems_t4", "보석 궤짝", "보석 2,750개 + 보너스 550개. 상품마다 처음 한 번은 기본 보석만큼 더 받아요.", 33000, 2750, 550),
            new Item("gems_t5", "보석 수레", "보석 4,900개 + 보너스 1,100개. 상품마다 처음 한 번은 기본 보석만큼 더 받아요.", 59000, 4900, 1100),
            new Item("gems_t6", "보석 보물고", "보석 9,900개 + 보너스 2,600개. 상품마다 처음 한 번은 기본 보석만큼 더 받아요.", 119000, 9900, 2600),
        };

        public static Item Find(string id)
        {
            foreach (Item item in All)
                if (item.Id == id) return item;
            return null;
        }
    }
}
