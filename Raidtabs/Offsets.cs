﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PKHeX.Core;
using pkNX.Structures.FlatBuffers;
namespace pk9reader
{
    public class Offsets
    {
        public const string ScarletID = "0100A3D008C5C000";
        public const string VioletID = "01008F6008C5E000";
        public static byte[][] GetEventEncounterDataFromSAV(PKHeX.Core.SAV9SV sav2)
        {
            byte[][] res = null!;
            var type2list = new List<byte[]>();
            var type3list = new List<byte[]>();

            var KBCATRaidEnemyArray = sav2.Accessor.FindOrDefault(Blocks.KBCATRaidEnemyArray.Key).Data;

            var tableEncounters = FlatBufferConverter.DeserializeFrom<DeliveryRaidEnemyArray>(KBCATRaidEnemyArray);

            var byGroupID = tableEncounters.Table
                .Where(z => z.RaidEnemyinfo.Rate != 0)
                .GroupBy(z => z.RaidEnemyinfo.DeliveryGroupID);

            bool isNot7Star = false;
            foreach (var group in byGroupID)
            {
                var items = group.ToArray();
                if (items.Any(z => z.RaidEnemyinfo.Difficulty > 7))
                    throw new Exception($"Undocumented difficulty {items.First(z => z.RaidEnemyinfo.Difficulty > 7).RaidEnemyinfo.Difficulty}");

                if (items.All(z => z.RaidEnemyinfo.Difficulty == 7))
                {
                    if (items.Any(z => z.RaidEnemyinfo.CaptureRate != 2))
                        throw new Exception($"Undocumented 7 star capture rate {items.First(z => z.RaidEnemyinfo.CaptureRate != 2).RaidEnemyinfo.CaptureRate}");
                    AddToList(items, type3list, RaidSerializationFormat.Type3);
                    continue;
                }

                if (items.Any(z => z.RaidEnemyinfo.Difficulty == 7))
                    throw new Exception($"Mixed difficulty {items.First(z => z.RaidEnemyinfo.Difficulty > 7).RaidEnemyinfo.Difficulty}");
               // if (isNot7Star)
                //    throw new Exception("Already saw a not-7-star group. How do we differentiate this slot determination from prior?");
                isNot7Star = true;
                AddToList(items, type2list, RaidSerializationFormat.Type2);
            }

            res = new byte[][] { type2list.SelectMany(z => z).ToArray(), type3list.SelectMany(z => z).ToArray() };
            return res;
        }

        private static void AddToList(IReadOnlyCollection<DeliveryRaidEnemyTable> table, List<byte[]> list, RaidSerializationFormat format)
        {
            // Get the total weight for each stage of star count
            Span<ushort> weightTotalS = stackalloc ushort[StageStars.Length];
            Span<ushort> weightTotalV = stackalloc ushort[StageStars.Length];
            foreach (var enc in table)
            {
                var info = enc.RaidEnemyinfo;
                if (info.Rate == 0)
                    continue;
                var difficulty = info.Difficulty;
                for (int stage = 0; stage < StageStars.Length; stage++)
                {
                    if (!StageStars[stage].Contains(difficulty))
                        continue;
                    if (info.RomVer != RaidRomType.TYPE_B)
                        weightTotalS[stage] += (ushort)info.Rate;
                    if (info.RomVer != RaidRomType.TYPE_A)
                        weightTotalV[stage] += (ushort)info.Rate;
                }
            }

            Span<ushort> weightMinS = stackalloc ushort[StageStars.Length];
            Span<ushort> weightMinV = stackalloc ushort[StageStars.Length];
            foreach (var enc in table)
            {
                var info = enc.RaidEnemyinfo;
                if (info.Rate == 0)
                    continue;
                var difficulty = info.Difficulty;
                TryAddToPickle(info, list, format, weightTotalS, weightTotalV, weightMinS, weightMinV);
                for (int stage = 0; stage < StageStars.Length; stage++)
                {
                    if (!StageStars[stage].Contains(difficulty))
                        continue;
                    if (info.RomVer != RaidRomType.TYPE_B)
                        weightMinS[stage] += (ushort)info.Rate;
                    if (info.RomVer != RaidRomType.TYPE_A)
                        weightMinV[stage] += (ushort)info.Rate;
                }
            }
        }

        private static void TryAddToPickle(RaidEnemyInfo enc, ICollection<byte[]> list, RaidSerializationFormat format,
            ReadOnlySpan<ushort> totalS, ReadOnlySpan<ushort> totalV, ReadOnlySpan<ushort> minS, ReadOnlySpan<ushort> minV)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            enc.SerializePKHeX(bw, (byte)enc.Difficulty, enc.Rate, format);
            for (int stage = 0; stage < StageStars.Length; stage++)
            {
                bool noTotal = !StageStars[stage].Contains(enc.Difficulty);
                ushort mS = minS[stage];
                ushort mV = minV[stage];
                bw.Write(noTotal ? (ushort)0 : mS);
                bw.Write(noTotal ? (ushort)0 : mV);
                bw.Write(noTotal ? (ushort)0 : totalS[stage]);
                bw.Write(noTotal ? (ushort)0 : totalV[stage]);
            }

            if (format == RaidSerializationFormat.Type3)
                enc.SerializeType3(bw);

           enc.SerializeTeraFinder(bw);

            var bin = ms.ToArray();
            if (!list.Any(z => z.SequenceEqual(bin)))
                list.Add(bin);
        }

        private static readonly int[][] StageStars =
        {
            new [] { 1, 2 },
            new [] { 1, 2, 3 },
            new [] { 1, 2, 3, 4 },
            new [] { 3, 4, 5, 6, 7 },
        };
    }

}
