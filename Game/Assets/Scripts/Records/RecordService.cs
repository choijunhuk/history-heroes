namespace HistoryHeroes.Records
{
    /// <summary>기록 변경은 호출한 곳의 SaveSystem.Commit으로 한꺼번에 저장한다.</summary>
    public static class RecordService
    {
        public static bool SaveToSlot(SaveData save, RunRecord record, int slot)
        {
            if (!CanAdd(save, record) || slot < 0 || slot >= save.recordSlots) return false;
            RunRecord old = save.RecordInSlot(slot);
            if (old != null && old.locked) return false;

            record.slot = slot;
            if (old != null) save.records.Remove(old);
            save.records.Add(record);
            save.relics.recordsSavedTotal++;
            return true;
        }

        public static bool SaveToTemp(SaveData save, RunRecord record)
        {
            if (!CanAdd(save, record)) return false;
            record.slot = -1;
            record.locked = false;
            save.tempRecords.Clear();
            save.tempRecords.Add(record);
            save.relics.recordsSavedTotal++;
            return true;
        }

        public static bool MoveTempToArchive(SaveData save)
        {
            if (save == null || save.TempRecord == null) return false;
            int slot = save.FirstFreeSlot();
            if (slot < 0) return false;
            RunRecord record = save.TempRecord;
            record.slot = slot;
            save.records.Add(record);
            save.tempRecords.Clear();
            return true;
        }

        public static bool Delete(SaveData save, int slot)
        {
            RunRecord record = save?.RecordInSlot(slot);
            return record != null && !record.locked && save.records.Remove(record);
        }

        public static bool ToggleLock(SaveData save, int slot)
        {
            RunRecord record = save?.RecordInSlot(slot);
            if (record == null) return false;
            record.locked = !record.locked;
            return true;
        }

        /// <summary>패치 뒤 다시 고르기는 같은 기록의 스킬만 바꾼다. 잠금·장비·격파 이력은 그대로 둔다.</summary>
        public static bool Reselect(SaveData save, string recordId, RunRecord replacement)
        {
            RunRecord record = save?.FindRecord(recordId);
            if (record == null || replacement == null || replacement.skills == null) return false;
            record.skills = replacement.skills;
            record.capacity = replacement.capacity;
            record.balanceVersion = replacement.balanceVersion;
            return true;
        }

        private static bool CanAdd(SaveData save, RunRecord record) =>
            save != null && record != null && !string.IsNullOrEmpty(record.id) && save.FindRecord(record.id) == null;
    }
}
