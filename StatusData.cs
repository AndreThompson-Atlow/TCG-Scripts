using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{

    public enum StatusType
    {
        None = 0,

        AddAttack = 4,      //Attack status can be used for attack boost limited for X turns 
        AddDefense = 3,
        AddHP = 5,          //HP status can be used for hp boost limited for X turns 
        AddManaCost = 6,    //Mana Cost status can be used for mana cost increase/reduction limited for X turns 
        AddAgility = 7,

        Stealth = 10,       //Cant be attacked until do action
        Invincibility = 12, //Cant be attacked for X turns
        Shell = 13,         //Receives no damage the first time
        Protection = 14,    //Taunt, gives Protected to other cards
        Protected = 15,     //Cards that are protected by taunt
        Armor = 16,         //Receives less damage
        SpellImmunity = 18, //Cant be targeted/damaged by spells
        

        Deathtouch = 20,    //Kills when attacking a character | Lethal
        Fury = 22,          //Can attack twice per turn | Bloodlust
        Intimidate = 23,    // Target's don't counter attack
        Flying = 24,         //Can ignore taunt | Stealth?
        Trample = 26,         //Extra damage is assigned to player | Impact 
        LifeSteal = 28,      //Heal unit when fighting | Siphon
        Ranged = 27,        //Target doesnt counter when attacking unless also ranged | Ranged
        ChannelLife = 21,    //Heal player when fighting | Channel Life
        Tyrant = 25,        // Cannot be played alongside other units, nor can other units be played.
        Vassal = 26,        // Can be summoned alongside a Tyrant.
        Pierce = 19,        //Ignore Elemental Resistances/Immunities


        Bleeding = 201,     // DOT, unconscious at 50% HP, vulnerable to poison/infect
        Ablaze = 202,       // High DOT, spreads to allies after 4 turns
        Frozen = 203,       // INC, minor DOT, removed by Infernal attacks
        Petrified = 204,    // INC, reduced max HP
        Paralyzed = 205,    // INC, reduced defense
        Wet = 206,          // Less Infernal damage, more Lightning damage and more Frost damage
        Submerged = 207,    // INC, dies after 6 turns, immune to fire, weak to Lightning
        Silenced = 208,     // SEAL, attack reduced to 0
        Corrupted = 209,    // SEAL, minor DOT to controller
        Charmed = 210,      // INC, not element-specific
        Poisoned = 211,     // DOT, not element-specific
        Unconscious = 212,  // INC, immune to Sleep, unique triggers
        Asleep = 213,       // INC, removed by damage, Shadow turns to Nightmare
        Dazed = 214,        // INC for 1 turn
        Nightmare = 215,    // INC, DOT
        InfectedI = 216,    // DOT, becomes InfectedII after 2 turns
        InfectedII = 217,   // DOT, INC, becomes InfectedIII after 2 turns
        InfectedIII = 218,  // Increased DOT, INC, spreads to allies
        Encased = 219,      // INC, immune to non-Light, removed by Fire, Shatter-vulnerable
        Entombed = 220,     // INC, immune to non-Shadow, Crush-vulnerable
        Immobilized = 221,  // INC, unique triggers
        QuickSand = 222,    // Loses 1 agility per turn
        Erosion = 223,      // Loses 1 defense per turn
        Overclocked = 224,  // Speed +3, 1 damage per turn
        Enraged = 225,      // Must attack, no abilities, increased attack
        Panic = 226,         // INC, reduced defense
        Drowning = 227,     // Takes 50% of Max HP DMG per turn

        PhysicalResist = 300,
        PhysicalVoid = 301,
        PhysicalWeakness = 302,

        GunResist = 303,
        GunVoid = 304,
        GunWeakness = 305,

        InfernalResist = 306,
        InfernalVoid = 307,
        InfernalWeakness = 308,

        FrostResist = 309,
        FrostVoid = 310,
        FrostWeakness = 311,

        TerraResist = 312,
        TerraVoid = 313,
        TerraWeakness = 314,

        LightningResist = 315,
        LightningVoid = 316,
        LightningWeakness = 317,

        AquaResist = 318,
        AquaVoid = 319,
        AquaWeakness = 320,

        HolyResist = 321,
        HolyVoid = 322,
        HolyWeakness = 323,

        ShadowResist = 324,
        ShadowVoid = 325,
        ShadowWeakness = 326,


        // Legacy Support
        // TODO: Remove when possible

        VoidAll = 105,      // Void all damage dealt to this unit
        WeakAll = 106,      // Take an additional 2 damage from all sources
        ResistAll = 107,    // Take 2 less damage from all sources
        AllBane = 108,      // Deal an additional 2 damage to all sources
        AbsorbAll = 109,    // Absorb damage from all sources

        GodSlayer = 111, // +3 Damage to Gods
        DemonSlayer = 112, // +3 Damage to Demons
        AngelSlayer = 113, // +3 Damage to Angels
        ArmorSlayer = 114, // +3 Damage to Armored units
        VanguardSlayer = 115,  // +3 Damage to Vanguards
        KingSlayer = 116,  // +3 Damage to Lords
        DreadSlayer = 117, // +3 Damage to Dread units

        Chained = 118,     // In the underworld, Temporarily Banished by Hades

        Countered = 119,    // Has Been Countered
        CounteredToHand = 120,    // Counterspell, but return to hand
        CounteredToDeck = 121,    // Counterspell, but return to deck

        DisableEnemyWards = 122, //Opponents cannot activate wards
        SpellLimit = 123,        //Opponents can only play 1 non-unit per turn

        PhasedOut = 124,         //Temporarily removed from play by a phase ability

    }

    /// <summary>
    /// Defines all status effects data
    /// Status are effects that can be gained or lost with abilities, and that will affect gameplay
    /// Status can have a duration
    /// </summary>

    [CreateAssetMenu(fileName = "status", menuName = "TcgEngine/StatusData", order = 7)]
    public class StatusData : ScriptableObject
    {
        public StatusType effect;

        [Header("Display")]
        public string title;
        public Sprite icon;

        [TextArea(3, 5)]
        public string desc;

        [Header("FX")]
        public GameObject status_fx;

        [Header("AI")]
        public int hvalue;

        public static List<StatusData> status_list = new List<StatusData>();

        public string GetTitle()
        {
            return title;
        }

        public string GetDesc()
        {
            return GetDesc(1);
        }

        public string GetDesc(int value)
        {
            string des = desc.Replace("<value>", value.ToString());
            return des;
        }

        public static void Load(string folder = "")
        {
            if (status_list.Count == 0)
                status_list.AddRange(Resources.LoadAll<StatusData>(folder));
        }

        public static StatusData Get(StatusType effect)
        {
            foreach (StatusData status in GetAll())
            {
                if (status.effect == effect)
                    return status;
            }
            return null;
        }

        public static List<StatusData> GetAll()
        {
            return status_list;
        }
    }
}