"""공개 확률표가 실제 Unity 에셋 목록과 맞는지 확인한다."""

from datetime import date
from decimal import Decimal
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest

import export_probabilities as exporter


class ProbabilityExportTests(unittest.TestCase):
    def test_public_page_covers_every_equipment_and_relic(self):
        text = exporter.render(date(2026, 10, 31))
        equipment = exporter.referenced_items(exporter.DATA / "Equipment", exporter.DATA / "Equipment/EquipmentTable.asset", "items", "weaponBase")
        relics = exporter.referenced_items(exporter.DATA / "Relics", exporter.DATA / "Relics/RelicTable.asset", "relics", "setNames")
        self.assertEqual(42, len(equipment))
        self.assertEqual(24, len(relics))
        for item in equipment + relics:
            self.assertEqual(1, text.count(f"| {item['name']} |"), item["id"])
        self.assertIn("낱개·10회 열기를 함께 셉니다", text)
        self.assertIn("50번째는 전설 확정", text)

    def test_missing_asset_reference_stops_export(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            table = root / "Table.asset"
            table.write_text("  items:\n  - {guid: " + "f" * 32 + "}\n  weaponBase:\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "찾을 수 없습니다"):
                exporter.referenced_items(root, table, "items", "weaponBase")

    def test_all_box_rates_sum_to_one(self):
        source = (exporter.DATA / "Equipment/GachaTable.asset").read_text(encoding="utf-8")
        self.assertEqual(1, sum(exporter.rates(source, "normal", "premium")))
        self.assertEqual(1, sum(exporter.rates(source, "premium", "tenPullGuarantee")))

    def test_guarantee_keeps_natural_rare_and_redraws_only_lower_results(self):
        normal_premium = [Decimal("0.50"), Decimal("0.35"), Decimal("0.13"), Decimal("0.02")]
        redraw = [Decimal(0), Decimal(0), Decimal("0.867"), Decimal("0.133")]
        final = exporter.guaranteed_final_rates(normal_premium, redraw)
        self.assertEqual(Decimal("0.86695"), final[2])
        self.assertEqual(Decimal("0.13305"), final[3])
        self.assertEqual(1, sum(final))
        text = exporter.render(date(2026, 10, 31))
        self.assertIn("희귀 86.695%, 전설 13.305%", text)
        self.assertIn("희귀 86.7%·전설 13.3% 표로 다시", text)


if __name__ == "__main__":
    unittest.main()
