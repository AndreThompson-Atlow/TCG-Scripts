using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace TcgEngine
{
    public enum CardType
    {
        None = 0,
        Hero = 5,
        Character = 10,
        Spell = 20,
        Artifact = 30,
        Secret = 40,
        Equipment = 50,
    }

    public enum CardMythos
    {
        Generic = 0,
        Lusticia = 1,
        Adonai = 2,
        Nehmet = 3,
        Reikon = 4,
        Nordvind = 5
    }

    public enum CardFaction
    {
        Generic = 0
    }

    public enum CardNation
    {
        Generic = 0,
        Hellenica = 1,
    }

    public enum CardSubType
    {
        Spell = 0,
        Skill = 1,
        Item = 2,
        Miracle = 3,
        Fusion = 4,
        Entity = 5,
        Summoner = 6,
        None = 7
    }

    public enum Element
    {
        Kinetic,
        Ballistic,
        Arcane,
        Infernal,
        Frost,
        Terra,
        Verdant,
        Volt,
        Aqua,
        Gale,
        Holy,
        Umbral,
        Necrotic,
        Radiant,
        None
    }

    public enum ElementalAffinity
    {
        Weak,
        Neutral,
        Resist,
        Void
    }

    public enum UnitClass
    {
        DPS,
        Support,
        Tank,
        Generalist,
        Spellblade,
        Warden,
        Berserker
    }

    /// <summary>
    /// Defines all card data
    /// </summary>

    [CreateAssetMenu(fileName = "card", menuName = "TcgEngine/CardData", order = 5)]
    public class CardData : ScriptableObject
    {
        public string id;

        [Header("Display")]
        public string title;
        public Sprite art_full;
        public Sprite art_board;

        [Header("Stats")]
        public CardType type;
        public TeamData team;
        public CardMythos mythos = CardMythos.Generic;
        public CardNation nation = CardNation.Generic;
        public CardFaction faction = CardFaction.Generic;
        public CardSubType subtype = CardSubType.Entity;
        public Element primaryElement = Element.Kinetic;
        public RarityData rarity;
        public int rarityRank = 0;
        public UnitClass unit_class = UnitClass.DPS;

        public int mana;
        public int mp;
        public int attack;
        public int defense;
        public int agility;
        public int hp;
        public int hp_cost;
        public int level;

        [Header("Affinities")]
        public ElementalAffinity kineticAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity ballisticAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity arcaneAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity infernalAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity frostAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity terraAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity verdantAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity voltAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity aquaAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity galeAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity holyAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity umbralAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity necroticAffinity = ElementalAffinity.Neutral;
        public ElementalAffinity radiantAffinity = ElementalAffinity.Neutral;

        [Header("Traits")]
        public TraitData[] traits;
        public TraitStat[] stats;

        [Header("Abilities")]
        public AbilityData[] abilities;

        [Header("Card Text")]
        [TextArea(3, 5)]
        public string text;

        [Header("Description")]
        [TextArea(5, 10)]
        public string desc;

        [Header("FX")]
        public GameObject spawn_fx;
        public GameObject death_fx;
        public GameObject attack_fx;
        public GameObject damage_fx;
        public GameObject idle_fx;
        public AudioClip spawn_audio;
        public AudioClip death_audio;
        public AudioClip attack_audio;
        public AudioClip damage_audio;

        [Header("Availability")]
        public bool deckbuilding = false;
        public int cost = 100;
        public int deck_limit = 4;
        public PackData[] packs;

        public static List<CardData> card_list = new List<CardData>();                              //Faster access in loops
        public static Dictionary<string, CardData> card_dict = new Dictionary<string, CardData>();    //Faster access in Get(id)

        public static void Load(string folder = "")
        {
            if (card_list.Count == 0)
            {
                card_list.AddRange(Resources.LoadAll<CardData>(folder));

                foreach (CardData card in card_list)
                    card_dict.Add(card.id, card);
            }
        }

        public Sprite GetBoardArt(VariantData variant)
        {
            return art_board;
        }

        public Sprite GetFullArt(VariantData variant)
        {
            return art_full;
        }

        public string GetTitle()
        {
            return title;
        }

        public string GetText()
        {
            return text;
        }

        public string GetDesc()
        {
            return desc;
        }

        public string GetTypeId()
        {
            if (type == CardType.Hero)
                return "hero";
            if (type == CardType.Character)
                return "character";
            if (type == CardType.Artifact)
                return "artifact";
            if (type == CardType.Spell)
                return "spell";
            if (type == CardType.Secret)
                return "secret";
            if (type == CardType.Equipment)
                return "equipment";
            return "";
        }

        public ElementalAffinity GetElementalAffinity(Element element)
        {
            return element switch
            {
                Element.Kinetic => kineticAffinity,
                Element.Ballistic => ballisticAffinity,
                Element.Arcane => arcaneAffinity,
                Element.Infernal => infernalAffinity,
                Element.Frost => frostAffinity,
                Element.Terra => terraAffinity,
                Element.Verdant => verdantAffinity,
                Element.Volt => voltAffinity,
                Element.Aqua => aquaAffinity,
                Element.Gale => galeAffinity,
                Element.Holy => holyAffinity,
                Element.Umbral => umbralAffinity,
                Element.Necrotic => necroticAffinity,
                Element.Radiant => radiantAffinity,
                Element.None => ElementalAffinity.Neutral,
                _ => ElementalAffinity.Neutral,
            };
        }

        public string GetMythosAsString()
        {
            if (mythos == CardMythos.Generic)
            {
                return "generic";
            } 
            if (mythos == CardMythos.Lusticia)
            {
                return "lusticia";
            }
            return "generic";
        }

        public string GetNationAsString()
        {
            if (nation == CardNation.Generic)
            {
                return "generic";
            }
            if (nation == CardNation.Hellenica)
            {
                return "hellenica";
            }
            return "generic";
        }

        public int GetCardDeckLimit()
        {
            if(subtype == CardSubType.Summoner || subtype == CardSubType.Miracle)
            {
                return 1;
            }
            return deck_limit;
        }

        public string GetAbilitiesDesc()
        {
            string txt = "";
            foreach (AbilityData ability in abilities)
            {
                if (!string.IsNullOrWhiteSpace(ability.desc))
                    txt += "<b>" + ability.GetTitle() + ":</b> " + ability.GetDesc(this) + "\n";
            }
            return txt;
        }

        public bool IsCharacter()
        {
            return type == CardType.Character;
        }

        public bool IsSecret()
        {
            return type == CardType.Secret;
        }

        public bool IsBoardCard()
        {
            return type == CardType.Character || type == CardType.Artifact;
        }

        public bool IsRequireTarget()
        {
            return type == CardType.Equipment || IsRequireTargetSpell();
        }

        public bool IsRequireTargetSpell()
        {
            return type == CardType.Spell && HasAbility(AbilityTrigger.OnPlay, AbilityTarget.PlayTarget);
        }

        public bool IsEquipment()
        {
            return type == CardType.Equipment;
        }

        public bool HasTrait(string trait)
        {
            foreach (TraitData t in traits)
            {
                if (t.id == trait)
                    return true;
            }
            return false;
        }

        public bool HasTrait(TraitData trait)
        {
            if(trait != null)
                return HasTrait(trait.id);
            return false;
        }

        public bool HasStat(string trait)
        {
            if (stats == null)
                return false;

            foreach (TraitStat stat in stats)
            {
                if (stat.trait.id == trait)
                    return true;
            }
            return false;
        }

        public bool HasStat(TraitData trait)
        {
            if(trait != null)
                return HasStat(trait.id);
            return false;
        }

        public int GetStat(string trait_id)
        {
            if (stats == null)
                return 0;

            foreach (TraitStat stat in stats)
            {
                if (stat.trait.id == trait_id)
                    return stat.value;
            }
            return 0;
        }

        public int GetStat(TraitData trait)
        {
            if(trait != null)
                return GetStat(trait.id);
            return 0;
        }

        public bool HasAbility(AbilityData tability)
        {
            foreach (AbilityData ability in abilities)
            {
                if (ability && ability.id == tability.id)
                    return true;
            }
            return false;
        }

        public bool HasAbility(AbilityTrigger trigger)
        {
            foreach (AbilityData ability in abilities)
            {
                if (ability && ability.trigger == trigger)
                    return true;
            }
            return false;
        }

        public bool HasAbility(AbilityTrigger trigger, AbilityTarget target)
        {
            foreach (AbilityData ability in abilities)
            {
                if (ability && ability.trigger == trigger && ability.target == target)
                    return true;
            }
            return false;
        }

        public AbilityData GetAbility(AbilityTrigger trigger)
        {
            foreach (AbilityData ability in abilities)
            {
                if (ability && ability.trigger == trigger)
                    return ability;
            }
            return null;
        }

        public bool HasPack(PackData pack)
        {
            foreach (PackData apack in packs)
            {
                if (apack == pack)
                    return true;
            }
            return false;
        }

        public static CardData Get(string id)
        {
            if (id == null)
                return null;
            bool success = card_dict.TryGetValue(id, out CardData card);
            if (success)
                return card;
            return null;
        }

        public static List<CardData> GetAllDeckbuilding()
        {
            List<CardData> multi_list = new List<CardData>();
            foreach (CardData acard in GetAll())
            {
                if (acard.deckbuilding)
                    multi_list.Add(acard);
            }
            return multi_list;
        }

        public static List<CardData> GetAll(PackData pack)
        {
            List<CardData> multi_list = new List<CardData>();
            foreach (CardData acard in GetAll())
            {
                if (acard.HasPack(pack))
                    multi_list.Add(acard);
            }
            return multi_list;
        }

        public static List<CardData> GetAll()
        {
            return card_list;
        }
    }
}