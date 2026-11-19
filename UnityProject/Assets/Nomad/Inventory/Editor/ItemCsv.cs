using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nomad.Inventory.Editor
{
    internal static class ItemCsv
    {
        internal readonly struct Row
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string Description;
            public readonly int MaxStack;
            public readonly EquipmentType? Equipment;
            public readonly int AttackPower;
            public readonly int Defense;
            public readonly WeaponAttackMode AttackMode;
            public readonly float AttackRange;
            public readonly float AttackInterval;

            public Row(string id, string name, string description, int maxStack,
                EquipmentType? equipment = null, int attackPower = 0, int defense = 0,
                WeaponAttackMode attackMode = WeaponAttackMode.None, float attackRange = 0, float attackInterval = 0)
            {
                Id = id;
                Name = name;
                Description = description;
                MaxStack = maxStack;
                Equipment = equipment;
                AttackPower = attackPower;
                Defense = defense;
                AttackMode = attackMode;
                AttackRange = attackRange;
                AttackInterval = attackInterval;
            }
        }

        public static List<Row> Parse(string text)
        {
            var records = ReadRecords(text.TrimStart('\uFEFF'));
            string[] header = { "item_id", "display_name", "description", "max_stack" };
            string[] equipmentHeader = { "item_id", "display_name", "description", "max_stack", "equipment_type", "attack_power", "defense", "attack_mode", "attack_range", "attack_interval" };
            bool extended = records.Count > 0 && EqualHeader(records[0], equipmentHeader);
            if (records.Count < 2 || (!extended && !EqualHeader(records[0], header)))
                throw new FormatException("기본4열 또는 장비10열 헤더와 데이터가 1행 이상 필요합니다.");

            var result = new List<Row>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; index < records.Count; index++)
            {
                List<string> cells = records[index];
                if (cells.Count != (extended ? 10 : 4))
                    throw new FormatException($"레코드 {index + 1}: 헤더와 열 수가 다릅니다.");

                string id = cells[0];
                if (!Regex.IsMatch(id, @"\A[A-Za-z0-9][A-Za-z0-9_-]*\z") || !ids.Add(id))
                    throw new FormatException($"레코드 {index + 1}: ID가 잘못되었거나 중복되었습니다. {id}");

                if (string.IsNullOrWhiteSpace(cells[1]) ||
                    !int.TryParse(cells[3], NumberStyles.None, CultureInfo.InvariantCulture, out int maxStack) || maxStack < 1)
                    throw new FormatException($"레코드 {index + 1}: 이름과 양의 정수 max_stack이 필요합니다.");

                EquipmentType? equipment = null;
                int attack = 0;
                int defense = 0;
                WeaponAttackMode mode = WeaponAttackMode.None;
                float range = 0;
                float interval = 0;
                if (extended)
                {
                    if (!Enum.TryParse(cells[4], false, out EquipmentType type) ||
                        !Enum.IsDefined(typeof(EquipmentType), type) || type.ToString() != cells[4] ||
                        !int.TryParse(cells[5], NumberStyles.None, CultureInfo.InvariantCulture, out attack) ||
                        !int.TryParse(cells[6], NumberStyles.None, CultureInfo.InvariantCulture, out defense) ||
                        (type != EquipmentType.None && maxStack != 1) ||
                        (type == EquipmentType.Weapon ? attack <= 0 || defense != 0 : attack != 0) ||
                        (type == EquipmentType.None && defense != 0))
                        throw new FormatException($"레코드 {index + 1}: 장비 분류/수치/중첩 수가 잘못되었습니다.");

                    equipment = type;
                    if (!Enum.TryParse(cells[7], false, out mode) ||
                        !Enum.IsDefined(typeof(WeaponAttackMode), mode) || mode.ToString() != cells[7] ||
                        !float.TryParse(cells[8], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out range) ||
                        !float.TryParse(cells[9], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out interval) ||
                        float.IsInfinity(range) || float.IsInfinity(interval) ||
                        (type == EquipmentType.Weapon ? mode == WeaponAttackMode.None || range <= 0 || interval <= 0 :
                            mode != WeaponAttackMode.None || range != 0 || interval != 0))
                        throw new FormatException($"레코드 {index + 1}: 공격 방식/사거리/간격이 잘못되었습니다.");
                }

                result.Add(new Row(id, cells[1], cells[2], maxStack, equipment, attack, defense, mode, range, interval));
            }

            return result;
        }

        private static bool EqualHeader(List<string> actual, string[] expected)
        {
            if (actual.Count != expected.Length)
                return false;

            for (int index = 0; index < expected.Length; index++)
                if (actual[index] != expected[index])
                    return false;

            return true;
        }

        private static List<List<string>> ReadRecords(string text)
        {
            var records = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            bool closedQuote = false;

            for (int index = 0; index < text.Length; index++)
            {
                char c = text[index];
                if (quoted)
                {
                    if (c != '"')
                        cell.Append(c);
                    else if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        cell.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                        closedQuote = true;
                    }
                    continue;
                }

                if (c == ',' || c == '\r' || c == '\n')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    closedQuote = false;
                    if (c != ',')
                    {
                        records.Add(row);
                        row = new List<string>();
                        if (c == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                            index++;
                    }
                    continue;
                }

                if (closedQuote)
                    throw new FormatException("닫힌 인용부호 뒤에는 구분자 또는 줄바꿈만 올 수 있습니다.");

                if (c == '"')
                {
                    if (cell.Length != 0)
                        throw new FormatException("인용부호는 필드 처음에만 올 수 있습니다.");
                    quoted = true;
                }
                else
                    cell.Append(c);
            }

            if (quoted)
                throw new FormatException("닫히지 않은 인용부호가 있습니다.");

            if (cell.Length > 0 || row.Count > 0 || closedQuote)
            {
                row.Add(cell.ToString());
                records.Add(row);
            }

            return records;
        }
    }
}
