using System.Collections;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

namespace TcgEngine
{
    //Represent the current state of a card during the game (data only)

    [System.Serializable]
    public class Card
    {
        public string card_id;
        public string uid;
        public int player_id;
        public int original_player_id;
        public string variant_id;

        public Slot slot;
        public bool exhausted = false;
        public int damage = 0;
        public int lastHitDamageValue = 0;

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


        public int hp_cost = 0;
        public int mp = 0;
        public int attack = 0;
        public int defense = 0;
        public int agility = 0;
        public int hp = 0;
        public int mana = 0;

        public int availableActions = 0;
        public int availableHalfActions = 1; // Units begin with a half action available
        public int totalActions = 1;

        public int hp_cost_ongoing = 0;
        public int mp_ongoing = 0;
        public int attack_ongoing = 0;
        public int defense_ongoing = 0;
        public int agility_ongoing = 0;
        public int hp_ongoing = 0;
        public int mana_ongoing = 0;

        public string equipped_uid = null;
        public string phased_target = "";

        public List<CardTrait> traits = new List<CardTrait>();
        public List<CardTrait> ongoing_traits = new List<CardTrait>();

        public List<CardStatus> status = new List<CardStatus>();
        public List<CardStatus> ongoing_status = new List<CardStatus>();

        public List<string> abilities = new List<string>();
        public List<string> abilities_ongoing = new List<string>();

        public Dictionary<StatusType, int> StatusCounters { get; private set; } = new Dictionary<StatusType, int>();

        [System.NonSerialized] private int hash = 0;
        [System.NonSerialized] private CardData data = null;
        [System.NonSerialized] private VariantData vdata = null;
        [System.NonSerialized] private List<AbilityData> abilities_data = null;

        public Card(string card_id, string uid, int player_id) { this.card_id = card_id; this.uid = uid; this.player_id = player_id;this.original_player_id = player_id; }

        public virtual void Refresh() 
        { 
            exhausted = false;

            int actions = 0;

            if (agility > 0)
            {
                actions = 1;
            }

            availableActions = actions;
            availableHalfActions = 0;
            totalActions = actions;

            AdjustStatusesPassively(false);
        }
        public virtual void ClearOngoing()
        {
            ongoing_status.Clear();
            ongoing_traits.Clear();
            ClearOngoingAbility();

            // Reset all ongoing stat modifiers
            hp_cost_ongoing = 0;
            mp_ongoing = 0;
            attack_ongoing = 0;
            defense_ongoing = 0;
            agility_ongoing = 0;
            hp_ongoing = 0;
            mana_ongoing = 0;
        }

        public virtual void Clear()
        {
            ClearOngoing(); Refresh(); damage = 0; status.Clear(); 
            SetCard(CardData, VariantData); //Reset to initial stats
            equipped_uid = null;
        }

        public virtual int GetAttack() { return Mathf.Max(attack + attack_ongoing, 0); }
        public virtual int GetHP() { return Mathf.Max(hp + hp_ongoing - damage, 0); }
        public virtual int GetHPMax() { return Mathf.Max(hp + hp_ongoing, 0); }
        public virtual int GetMana() { return Mathf.Max(mana + mana_ongoing, 0); }
        public virtual int GetDefense() { return Mathf.Max(defense + defense_ongoing, 0); }
        public virtual int GetAgility() { return Mathf.Max(agility + agility_ongoing, 0); }
        public virtual int GetHPCost() { return Mathf.Max(hp_cost + hp_cost_ongoing, 0); }
        public virtual int GetMP() { return Mathf.Max(mp + mp_ongoing, 0); }

        public virtual void SetCard(CardData icard, VariantData cvariant)
        {
            data = icard;
            card_id = icard.id;
            variant_id = cvariant.id;

            // Set all stats from CardData
            attack = icard.attack;
            defense = icard.defense;
            agility = icard.agility;
            hp = icard.hp;
            mana = icard.mana;
            hp_cost = icard.hp_cost;
            mp = icard.mp;

            // Set all affinities from CardData
            kineticAffinity = icard.kineticAffinity;
            ballisticAffinity = icard.ballisticAffinity;
            arcaneAffinity = icard.arcaneAffinity;
            infernalAffinity = icard.infernalAffinity;
            frostAffinity = icard.frostAffinity;
            terraAffinity = icard.terraAffinity;
            verdantAffinity = icard.verdantAffinity;
            voltAffinity = icard.voltAffinity;
            aquaAffinity = icard.aquaAffinity;
            galeAffinity = icard.galeAffinity;
            holyAffinity = icard.holyAffinity;
            umbralAffinity = icard.umbralAffinity;
            necroticAffinity = icard.necroticAffinity;
            radiantAffinity = icard.radiantAffinity;

            // Reset all ongoing modifiers
            attack_ongoing = 0;
            defense_ongoing = 0;
            agility_ongoing = 0;
            hp_ongoing = 0;
            mana_ongoing = 0;
            hp_cost_ongoing = 0;
            mp_ongoing = 0;

            SetTraits(icard);
            SetAbilities(icard);
        }

        public void SetTraits(CardData icard)
        {
            traits.Clear();
            foreach (TraitData trait in icard.traits)
                SetTrait(trait.id, 0);
            if (icard.stats != null)
            {
                foreach (TraitStat stat in icard.stats)
                    SetTrait(stat.trait.id, stat.value);
            }
        }

        public void SetAbilities(CardData icard)
        {
            abilities.Clear();
            abilities_ongoing.Clear();
            if (abilities_data != null)
                abilities_data.Clear();
            foreach (AbilityData ability in icard.abilities)
                AddAbility(ability);
        }

        //------ Adjust Statuses     ---------

        public void AdjustStatusesPassively(bool shouldIncrementCounters)
        {
            List<StatusType> statusesToRemove = new List<StatusType>();
            List<StatusType> statusesToAdd = new List<StatusType>();

            // Counter to track how long a unit has been submerged
            if (!StatusCounters.ContainsKey(StatusType.Submerged))
            {
                StatusCounters[StatusType.Submerged] = 0;
            }

            foreach (CardStatus cardStatus in GetAllStatus())
            {
                StatusType status = cardStatus.type;
                switch (status)
                {
                    case StatusType.Bleeding:
                        if (GetHP() < GetHPMax() * 0.5f)
                        {
                            statusesToAdd.Add(StatusType.Unconscious);
                        }
                        break;

                    case StatusType.Submerged:
                        if (shouldIncrementCounters)
                        {
                            StatusCounters[StatusType.Submerged]++;
                            if (StatusCounters[StatusType.Submerged] >= 6)  // Assuming 6 turns of Submerged leads to Drowning
                            {
                                statusesToAdd.Add(StatusType.Drowning);
                            }
                        }
                        break;

                        // Add other passive status checks here
                }
            }

            // Check for status removals
            if (!HasStatus(StatusType.Submerged))
            {
                statusesToRemove.Add(StatusType.Drowning);
            }

            // Apply the status changes
            foreach (StatusType status in statusesToRemove)
            {
                RemoveStatus(status);
                if (status == StatusType.Submerged)
                {
                    StatusCounters.Remove(StatusType.Submerged);
                    RemoveStatus(StatusType.Drowning);
                }
            }

            foreach (StatusType status in statusesToAdd)
            {
                AddStatus(status, 0, 0);
            }
        }

        //------ Custom Traits/Stats ---------

        public void SetTrait(string id, int value)
        {
            CardTrait trait = GetTrait(id);
            if (trait != null)
            {
                trait.value = value;
            }
            else
            {
                trait = new CardTrait(id, value);
                traits.Add(trait);
            }
        }

        public void AddTrait(string id, int value)
        {
            CardTrait trait = GetTrait(id);
            if (trait != null)
                trait.value += value;
            else
                SetTrait(id, value);
        }

        public void AddOngoingTrait(string id, int value)
        {
            CardTrait trait = GetOngoingTrait(id);
            if (trait != null)
            {
                trait.value += value;
            }
            else
            {
                trait = new CardTrait(id, value);
                ongoing_traits.Add(trait);
            }
        }

        public void RemoveTrait(string id)
        {
            for (int i = traits.Count - 1; i >= 0; i--)
            {
                if (traits[i].id == id)
                    traits.RemoveAt(i);
            }
        }

        public CardTrait GetTrait(string id)
        {
            foreach (CardTrait trait in traits)
            {
                if (trait.id == id)
                    return trait;
            }
            return null;
        }

        public CardTrait GetOngoingTrait(string id)
        {
            foreach (CardTrait trait in ongoing_traits)
            {
                if (trait.id == id)
                    return trait;
            }
            return null;
        }

        public int GetTraitValue(TraitData trait)
        {
            if (trait != null)
                return GetTraitValue(trait.id);
            return 0;
        }

        public virtual int GetTraitValue(string id)
        {
            int val = 0;
            CardTrait stat1 = GetTrait(id);
            CardTrait stat2 = GetOngoingTrait(id);
            if (stat1 != null)
                val += stat1.value;
            if (stat2 != null)
                val += stat2.value;
            return val;
        }

        public bool HasTrait(TraitData trait)
        {
            if (trait != null)
                return HasTrait(trait.id);
            return false;
        }

        public bool HasTrait(string id)
        {
            return GetTrait(id) != null || GetOngoingTrait(id) != null;
        }

        public List<CardTrait> GetAllTraits()
        {
            List<CardTrait> all_traits = new List<CardTrait>();
            all_traits.AddRange(traits);
            all_traits.AddRange(ongoing_traits);
            return all_traits;
        }
        
        //Alternate names since traits/stats are stored in same var
        public void SetStat(string id, int value) => SetTrait(id, value);
        public void AddStat(string id, int value) => AddTrait(id, value);
        public void AddOngoingStat(string id, int value) => AddOngoingTrait(id, value);
        public void RemoveStat(string id) => RemoveTrait(id);
        public int GetStatValue(TraitData trait) => GetTraitValue(trait);
        public int GetStatValue(string id) => GetTraitValue(id);
        public bool HasStat(TraitData trait) => HasTrait(trait);
        public bool HasStat(string id) => HasTrait(id);
        public List<CardTrait> GetAllStats() => GetAllTraits();

        //------  Status Effects ---------

        public void AddStatus(StatusData status, int value, int duration)
        {
            if (status != null)
                AddStatus(status.effect, value, duration);
        }

        public void AddOngoingStatus(StatusData status, int value)
        {
            if (status != null)
                AddOngoingStatus(status.effect, value);
        }

        public void AddStatus(StatusType type, int value, int duration)
        {
            if (type != StatusType.None)
            {
                CardStatus status = GetStatus(type);
                if (status == null)
                {
                    status = new CardStatus(type, value, duration);
                    this.status.Add(status);
                }
                else
                {
                    status.value += value;
                    status.duration = Mathf.Max(status.duration, duration);
                    status.permanent = status.permanent || duration == 0;
                }
            }
        }

        public void AddOngoingStatus(StatusType type, int value)
        {
            if (type != StatusType.None)
            {
                CardStatus status = GetOngoingStatus(type);
                if (status == null)
                {
                    status = new CardStatus(type, value, 0);
                    ongoing_status.Add(status);
                }
                else
                {
                    status.value += value;
                }
            }
        }

        public void RemoveStatus(StatusType type)
        {
            for (int i = status.Count - 1; i >= 0; i--)
            {
                if (status[i].type == type)
                    status.RemoveAt(i);
            }
        }

        public List<CardStatus> GetAllStatus()
        {
            List<CardStatus> all_status = new List<CardStatus>();
            all_status.AddRange(status);
            all_status.AddRange(ongoing_status);
            return all_status;
        }

        public bool HasStatus(StatusType type)
        {
            return GetStatus(type) != null || GetOngoingStatus(type) != null;
        }

        public CardStatus GetStatus(StatusType type)
        {
            foreach (CardStatus status in status)
            {
                if (status.type == type)
                    return status;
            }
            return null;
        }

        public CardStatus GetOngoingStatus(StatusType type)
        {
            foreach (CardStatus status in ongoing_status)
            {
                if (status.type == type)
                    return status;
            }
            return null;
        }

        public virtual int GetStatusValue(StatusType type)
        {
            CardStatus status1 = GetStatus(type);
            CardStatus status2 = GetOngoingStatus(type);
            int v1 = status1 != null ? status1.value : 0;
            int v2 = status2 != null ? status2.value : 0;
            return v1 + v2;
        }

        public virtual void ReduceStatusDurations()
        {
            for (int i = status.Count - 1; i >= 0; i--)
            {
                if (!status[i].permanent)
                {
                    status[i].duration -= 1;
                    if (status[i].duration <= 0)
                        status.RemoveAt(i);
                }
            }
        }

        //----- Abilities ------------

        public void AddAbility(AbilityData ability)
        {
            abilities.Add(ability.id);
			if (abilities_data != null)
				abilities_data.Add(ability);
        }

        public void RemoveAbility(AbilityData ability)
        {
            abilities.Remove(ability.id);
            if (abilities_data != null)
                abilities_data.Remove(ability);
        }

        public void AddOngoingAbility(AbilityData ability)
        {
            if (!abilities_ongoing.Contains(ability.id) && !abilities.Contains(ability.id))
            {
                abilities_ongoing.Add(ability.id);
                if (abilities_data != null)
                    abilities_data.Add(ability);
            }
        }

        public void ClearOngoingAbility()
        {
            if (abilities_data != null)
            {
                for (int i = abilities_data.Count - 1; i >= 0; i--)
                {
                    AbilityData ability = abilities_data[i];
                    if (abilities_ongoing.Contains(ability.id))
                        abilities_data.RemoveAt(i);
                }
            }

            abilities_ongoing.Clear();
        }

        public AbilityData GetAbility(AbilityTrigger trigger)
        {
            foreach (AbilityData iability in GetAbilities())
            {
                if (iability.trigger == trigger)
                    return iability;
            }
            return null;
        }

        public bool HasAbility(AbilityData ability)
        {
            foreach (AbilityData iability in GetAbilities())
            {
                if (iability.id == ability.id)
                    return true;
            }
            return false;
        }

        public bool HasAbility(AbilityTrigger trigger)
        {
            AbilityData iability = GetAbility(trigger);
            if (iability != null)
                return true;
            return false;
        }

        public bool HasAbility(AbilityTrigger trigger, AbilityTarget target)
        {
            foreach (AbilityData iability in GetAbilities())
            {
                if (iability.trigger == trigger && iability.target == target)
                    return true;
            }
            return false;
        }

        public bool HasActiveAbility(Game data, AbilityTrigger trigger)
        {
            AbilityData iability = GetAbility(trigger);
            if (iability != null && CanDoAbilities() && iability.AreTriggerConditionsMet(data, this))
                return true;
            return false;
        }

        public bool AreAbilityConditionsMet(AbilityTrigger ability_trigger, Game data, Card caster, Card triggerer)
        {
            foreach (AbilityData ability in GetAbilities())
            {
                if (ability && ability.trigger == ability_trigger && ability.AreTriggerConditionsMet(data, caster, triggerer))
                    return true;
            }
            return false;
        }

        public List<AbilityData> GetAbilities()
        {
            //Load abilities data, important to do this here since this array will be null after being sent through networking (cant serialize it)
            if (abilities_data == null)
            {
                abilities_data = new List<AbilityData>(abilities.Count + abilities_ongoing.Count);
                for (int i = 0; i < abilities.Count; i++)
                    abilities_data.Add(AbilityData.Get(abilities[i]));
                for (int i = 0; i < abilities_ongoing.Count; i++)
                    abilities_data.Add(AbilityData.Get(abilities_ongoing[i]));
            }

            //Return
            return abilities_data;
        }

        public virtual bool CanPayAbility(AbilityData ability)
        {
            bool hasActions = totalActions > 0;
            int manaCost = ability.ability_type == AbilityType.Spell ? ability.mana_cost : 0;
            int hpCost = ability.ability_type == AbilityType.Skill ? ability.mana_cost : 0;
            return hasActions && GetMP() >= manaCost && GetHP() >= hpCost;
        }

        public virtual bool CanPayAbility(Card card, AbilityData ability)
        {
            bool exhaust = !card.exhausted || !ability.exhaust;
            return exhaust && mana >= ability.mana_cost;
        }

        //---- Action Check ---------

        public virtual bool CanAttack(bool skip_cost = false)
        {
            totalActions = availableActions + availableHalfActions;

            if (HasStatus(StatusType.Paralyzed))
                return false;
            if (!skip_cost && (totalActions < 1))
                return false; //no more action
            return true;
        }

        public virtual bool CanMove(bool skip_cost = false)
        {
            //In demo we can move freely, since it has no effect
            //if (HasStatusEffect(StatusEffect.Paralysed))
            //   return false;
            //if (!skip_cost && exhausted)
            //    return false; //no more action
            return false; // No movement mechanic right now
        }

        public virtual bool IsIncapacitated()
        {
            return HasStatus(StatusType.Paralyzed) || HasStatus(StatusType.Frozen) || HasStatus(StatusType.Submerged) || HasStatus(StatusType.Encased) || HasStatus(StatusType.Asleep) || HasStatus(StatusType.Unconscious);
        }

        public virtual bool IsMuted()
        {
            return HasStatus(StatusType.Silenced);
        }

        public virtual bool CanDoActivatedAbilities(bool inCheckCardActions = true)
        {
            if (IsMuted() || IsIncapacitated())
                return false;
            if (totalActions < 1 && inCheckCardActions)
                return false; //no more action

            return true;
        }

        public virtual bool CanDoAbilities()
        {
            if (IsMuted())
                return false;
            return true;
        }

        public virtual bool CanDoAnyAction()
        {
            return CanAttack() || CanDoActivatedAbilities();
        }

        //----------------

        public CardData CardData 
        { 
            get { 
                if(data == null || data.id != card_id)
                    data = CardData.Get(card_id); //Optimization, store for future use
                return data;
            } 
        }

        public VariantData VariantData
        {
            get
            {
                if (vdata == null || vdata.id != variant_id)
                    vdata = VariantData.Get(variant_id); //Optimization, store for future use
                return vdata;
            }
        }

        public CardData Data => CardData; //Alternate name

        public int Hash
        {
            get {
                if (hash == 0)
                    hash = Mathf.Abs(uid.GetHashCode()); //Optimization, store for future use
                return hash;
            }
        }

        public static Card Create(CardData icard, VariantData ivariant, Player player)
        {
            return Create(icard, ivariant, player, GameTool.GenerateRandomID(11, 15));
        }

        public static Card Create(CardData icard, VariantData ivariant, Player player, string uid)
        {
            Card card = new Card(icard.id, uid, player.player_id);
            card.SetCard(icard, ivariant);
            player.cards_all[card.uid] = card;
            return card;
        }

        public static Card CloneNew(Card source)
        {
            Card card = new Card(source.card_id, source.uid, source.player_id);
            Clone(source, card);
            return card;
        }

        //Clone all card variables into another var, used mostly by the AI when building a prediction tree
        public static void Clone(Card source, Card dest)
        {
            dest.card_id = source.card_id;
            dest.uid = source.uid;
            dest.player_id = source.player_id;

            dest.StatusCounters = source.StatusCounters;

            dest.variant_id = source.variant_id;
            dest.slot = source.slot;
            dest.exhausted = source.exhausted;
            dest.damage = source.damage;
            dest.lastHitDamageValue = source.lastHitDamageValue;

            dest.availableActions = source.availableActions;
            dest.availableHalfActions = source.availableHalfActions;
            dest.totalActions = source.totalActions;

            dest.kineticAffinity = source.kineticAffinity;
            dest.ballisticAffinity = source.ballisticAffinity;
            dest.arcaneAffinity = source.arcaneAffinity;
            dest.infernalAffinity = source.infernalAffinity;
            dest.frostAffinity = source.frostAffinity;
            dest.terraAffinity = source.terraAffinity;
            dest.verdantAffinity = source.verdantAffinity;
            dest.voltAffinity = source.voltAffinity;
            dest.aquaAffinity = source.aquaAffinity;
            dest.galeAffinity = source.galeAffinity;
            dest.holyAffinity = source.holyAffinity;
            dest.umbralAffinity = source.umbralAffinity;
            dest.necroticAffinity = source.necroticAffinity;
            dest.radiantAffinity = source.radiantAffinity;

            // Clone base stats
            dest.attack = source.attack;
            dest.defense = source.defense;
            dest.agility = source.agility;
            dest.hp = source.hp;
            dest.mana = source.mana;
            dest.hp_cost = source.hp_cost;
            dest.mp = source.mp;

            // Clone ongoing modifiers
            dest.attack_ongoing = source.attack_ongoing;
            dest.defense_ongoing = source.defense_ongoing;
            dest.agility_ongoing = source.agility_ongoing;
            dest.hp_ongoing = source.hp_ongoing;
            dest.mana_ongoing = source.mana_ongoing;
            dest.hp_cost_ongoing = source.hp_cost_ongoing;
            dest.mp_ongoing = source.mp_ongoing;

            dest.equipped_uid = source.equipped_uid;

            CardTrait.CloneList(source.traits, dest.traits);
            CardTrait.CloneList(source.ongoing_traits, dest.ongoing_traits);
            CardStatus.CloneList(source.status, dest.status);
            CardStatus.CloneList(source.ongoing_status, dest.ongoing_status);
            GameTool.CloneList(source.abilities, dest.abilities); 
            GameTool.CloneList(source.abilities_ongoing, dest.abilities_ongoing); 
            GameTool.CloneListRefNull(source.abilities_data, ref dest.abilities_data); //No need to deep copy since AbilityData doesn't change dynamically, its just a reference
        }

        //Clone a var that could be null
        public static void CloneNull(Card source, ref Card dest)
        {
            //Source is null
            if (source == null)
            {
                dest = null;
                return;
            }

            //Dest is null
            if (dest == null)
            {
                dest = CloneNew(source);
                return;
            }

            //Both arent null, just clone
            Clone(source, dest);
        }

        //Clone dictionary completely
        public static void CloneDict(Dictionary<string, Card> source, Dictionary<string, Card> dest)
        {
            foreach (KeyValuePair<string, Card> pair in source)
            {
                bool valid = dest.TryGetValue(pair.Key, out Card val);
                if (valid)
                    Clone(pair.Value, val);
                else
                    dest[pair.Key] = CloneNew(pair.Value);
            }
        }

        //Clone list by keeping references from ref_dict
        public static void CloneListRef(Dictionary<string, Card> ref_dict, List<Card> source, List<Card> dest)
        {
            for (int i = 0; i < source.Count; i++)
            {
                Card scard = source[i];
                bool valid = ref_dict.TryGetValue(scard.uid, out Card rcard);
                if (valid)
                {
                    if (i < dest.Count)
                        dest[i] = rcard;
                    else
                        dest.Add(rcard);
                }
            }

            if(dest.Count > source.Count)
                dest.RemoveRange(source.Count, dest.Count - source.Count);
        }
    }

    [System.Serializable]
    public class CardStatus
    {
        public StatusType type;
        public int value;
        public int duration = 1;
        public bool permanent = true;

        [System.NonSerialized]
        private StatusData data = null;

        public CardStatus() { }

        public CardStatus(StatusType type, int value, int duration)
        {
            this.type = type;
            this.value = value;
            this.duration = duration;
            this.permanent = (duration == 0);
        }

        public StatusData StatusData { 
            get
            {
                if (data == null || data.effect != type)
                    data = StatusData.Get(type);
                return data;
            }
        }

        public StatusData Data => StatusData; //Alternate name

        public static CardStatus CloneNew(CardStatus copy)
        {
            CardStatus status = new CardStatus(copy.type, copy.value, copy.duration);
            status.permanent = copy.permanent;
            return status;
        }

        public static void Clone(CardStatus source, CardStatus dest)
        {
            dest.type = source.type;
            dest.value = source.value;
            dest.duration = source.duration;
            dest.permanent = source.permanent;
        }

        public static void CloneList(List<CardStatus> source, List<CardStatus> dest)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (i < dest.Count)
                    Clone(source[i], dest[i]);
                else
                    dest.Add(CloneNew(source[i]));
            }

            if (dest.Count > source.Count)
                dest.RemoveRange(source.Count, dest.Count - source.Count);
        }
    }

    [System.Serializable]
    public class CardTrait
    {
        public string id;
        public int value;

        [System.NonSerialized]
        private TraitData data = null;

        public CardTrait(string id, int value)
        {
            this.id = id;
            this.value = value;
        }

        public CardTrait(TraitData trait, int value)
        {
            this.id = trait.id;
            this.value = value;
        }

        public TraitData TraitData
        {
            get
            {
                if (data == null || data.id != id)
                    data = TraitData.Get(id);
                return data;
            }
        }

        public TraitData Data => TraitData; //Alternate name


        public static CardTrait CloneNew(CardTrait copy)
        {
            CardTrait status = new CardTrait(copy.id, copy.value);
            return status;
        }

        public static void Clone(CardTrait source, CardTrait dest)
        {
            dest.id = source.id;
            dest.value = source.value;
        }

        public static void CloneList(List<CardTrait> source, List<CardTrait> dest)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (i < dest.Count)
                    Clone(source[i], dest[i]);
                else
                    dest.Add(CloneNew(source[i]));
            }

            if (dest.Count > source.Count)
                dest.RemoveRange(source.Count, dest.Count - source.Count);
        }
    }
}
