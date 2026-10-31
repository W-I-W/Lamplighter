using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Внешность героя: индексы в палитрах. Не влияет на характеристики. Передаётся по сети как шесть чисел.</summary>
public sealed class Look
{
    public const int SkinN = 8, HairN = 7, HairColN = 10, ClothN = 12, VariantN = 4;
    public int Skin, Hair, HairCol, Cloth1 = 1, Cloth2 = 4, Variant;

    public static readonly Color[] SkinCols = { new("#fbe3cf"), new("#f0c8a0"), new("#e0ac7c"), new("#c58c5a"), new("#a56b3f"), new("#7d4a2a"), new("#5a3320"), new("#9ec9a0") };
    public static readonly Color[] HairCols = { new("#2a1c14"), new("#5a3a22"), new("#8a5a2c"), new("#c9954a"), new("#e8d27a"), new("#b8442a"), new("#d8d8e0"), new("#4a6ac8"), new("#8a4ac8"), new("#3aa86a") };
    public static readonly Color[] ClothCols = { new("#c84a3a"), new("#4a6dc0"), new("#3a9a5a"), new("#d8a838"), new("#8a4ac8"), new("#2f3a56"), new("#e8e4d8"), new("#d8703a"), new("#3ab0b8"), new("#c8548a"), new("#6a6a78"), new("#7a5230") };
    public static readonly string[] HairNames = { "Короткая", "Ёжик", "Длинная", "Хвост", "Капюшон", "Шапка", "Лысый" };
    public static readonly string[] VariantNames = { "Куртка", "Плащ", "Туника", "Разведчик" };

    public Look Clone() => (Look)MemberwiseClone();

    public void Clamp()
    {
        Skin = Math.Clamp(Skin, 0, SkinN - 1); Hair = Math.Clamp(Hair, 0, HairN - 1); HairCol = Math.Clamp(HairCol, 0, HairColN - 1);
        Cloth1 = Math.Clamp(Cloth1, 0, ClothN - 1); Cloth2 = Math.Clamp(Cloth2, 0, ClothN - 1); Variant = Math.Clamp(Variant, 0, VariantN - 1);
    }

    public int[] ToInts() => new[] { Skin, Hair, HairCol, Cloth1, Cloth2, Variant };

    public static Look FromInts(int[] a)
    {
        var l = new Look(); if (a != null && a.Length >= 6) { l.Skin = a[0]; l.Hair = a[1]; l.HairCol = a[2]; l.Cloth1 = a[3]; l.Cloth2 = a[4]; l.Variant = a[5]; }
        l.Clamp(); return l;
    }

    public static Look Random(RandomNumberGenerator r)
    {
        var l = new Look { Skin = r.RandiRange(0, SkinN - 2), Hair = r.RandiRange(0, HairN - 1), HairCol = r.RandiRange(0, HairColN - 1), Cloth1 = r.RandiRange(0, ClothN - 1), Cloth2 = r.RandiRange(0, ClothN - 1), Variant = r.RandiRange(0, VariantN - 1) };
        if (l.Cloth2 == l.Cloth1) l.Cloth2 = (l.Cloth1 + 5) % ClothN; return l;
    }
}

/// <summary>Положение героя в одном конкретном мире (хранится в файле героя по WorldId).</summary>
public sealed class PerWorld { public float X, Y; public bool Has; public string Stamp = ""; }

/// <summary>Всё, что принадлежит герою и едет с ним из мира в мир: предметы, оружие, экипировка, навыки, личные характеристики.</summary>
public sealed class CharacterSave
{
    public int Version = 1; public string Id = "", Name = "", Stamp = ""; public long Created; public Look Look = new();
    public float PlaySecs, Hp = 60, MaxHp = 60, Xp; public int Level = 1, Tier, Kills, HeroLamp;
    public long Ann; public int[] Inv, ModInv; public string[] SkK; public int[] SkV;
    public string[] Hot, Tools, WId, Relics, Equipped, BpDone, FragK, BsK, Hints, Found; public int HotSel, CurW; public string CurTool; public int[][] WMods; public int[] WLv; public int AxeLv; public int[] FragV, BsV;
    public int LegendMask, CosmMask = 1, CosmSel;
    public Dictionary<string, PerWorld> Worlds = new();
}

/// <summary>Всё, что принадлежит миру и остаётся в нём независимо от героя.</summary>
public sealed class WorldSave
{
    public int Version = 1, GenV, Seed, Difficulty; public string Id = "", Name = "", Stamp = ""; public long Created; public float PlaySecs;
    public string Obj, Explored; public bool[] Lit; public byte[] AncDone, PoiState;
    public int[] DropItem, DropCount; public float[] DropX, DropY;               // предметы на земле
    public int[] Stock; public int BossMask, Beacon, TradeEpoch, ExtraV, VillEvents, ForgeMade, NextVillId, LoreMask, RiftUnlocked = 1, RiftFirst, RiftClears; public bool Finished;
    public int[] BT, BX, BY, BL, BPrio, BMode, BLocal; public bool[] BOff, BForced; public int[] LX, LY, LK, LL, LD;
    public string[] UpCats, UpNew, Bought; public int[] CutB, PlacedK; public float[] Demand; public float EvNext;
    public string[] EvK; public float[] EvX, EvY, EvT, EvL, EvV; public int[] EvS, EvW, EvSeed;
    public VillSave[] Vills; public int[] ManualB;
}

public sealed class VillSave { public int Id, Work = -1, Home = -1, Talk; public string Name = "", Prof = "", Src = ""; public float X, Y; }

/// <summary>Краткие сведения для карточек списков (читаются из файла без запуска игры).</summary>
public sealed class CharInfo
{
    public string Id = "", Name = "", Stamp = ""; public Look Look = new(); public float PlaySecs; public int Level = 1, Tier, SkillPts, Weapons, Relics; public float MaxHp = 60; public string Weapon = "", Tool = "";
    public long Created; public bool Ok = true, HasLamp;
}

public sealed class WorldInfo
{
    public string Id = "", Name = "", Stamp = ""; public int Seed, Difficulty, Beacon = 1, Explored, Lit; public float PlaySecs; public long Created; public bool Finished, Ok = true;
}
