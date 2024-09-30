using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TcgEngine.Gameplay;
using UnityEngine;

namespace TcgEngine
{
    //Contains all gameplay state data that is sync across network

    [System.Serializable]
    public class Game
    {
        public string game_uid;
        public GameSettings settings;

        //Game state
        public int first_player = 0;
        public int current_player = 0;
        public int turn_count = 0;
        public float turn_timer = 0f;
        public int active_player_non_unit_casts_this_turn = 0;

        public GameState state = GameState.Connecting;
        public GamePhase phase = GamePhase.None;

        //Players
        public Player[] players;

        //Selector
        public SelectorType selector = SelectorType.None;
        public int selector_player_id = 0;
        public string selector_ability_id;
        public string selector_caster_uid;

        //Other reference values
        public string last_played;
        public string last_target;
        public string last_destroyed;
        public string last_summoned;
        public string ability_triggerer;
        public int rolled_value;

        //Other reference arrays 
        public HashSet<string> ability_played = new HashSet<string>();
        public HashSet<string> cards_attacked = new HashSet<string>();

        public Game() { }
        
        public Game(string uid, int nb_players)
        {
            this.game_uid = uid;
            players = new Player[nb_players];
            for (int i = 0; i < nb_players; i++)
                players[i] = new Player(i);
            settings = GameSettings.Default;
        }

        public virtual bool AreAllPlayersReady()
        {
            int ready = 0;
            foreach (Player player in players)
            {
                if (player.IsReady())
                    ready++;
            }
            return ready >= settings.nb_players;
        }

        public virtual bool AreAllPlayersConnected()
        {
            int ready = 0;
            foreach (Player player in players)
            {
                if (player.IsConnected())
                    ready++;
            }
            return ready >= settings.nb_players;
        }

        //Check if its player's turn
        public virtual bool IsPlayerTurn(Player player)
        {
            return IsPlayerActionTurn(player) || IsPlayerSelectorTurn(player);
        }

        public virtual bool IsPlayerActionTurn(Player player)
        {
            return player != null && current_player == player.player_id 
                && state == GameState.Play && selector == SelectorType.None;
        }

        public virtual bool IsPlayerSelectorTurn(Player player)
        {
            return player != null && selector_player_id == player.player_id 
                && state == GameState.Play && selector != SelectorType.None;
        }

        //Check if a card is allowed to be played in any slot
        public virtual bool CanPlayCardAnySlot(Card card, bool skip_cost = false)
        {
            return Slot.GetAll().Any(slot => CanPlayCard(card, slot, skip_cost));
        }

        //Check if a card can use any abilities
        public virtual bool CanUseAnyAbility(Card card, int boardZone = 0)
        {
            return card.GetAbilities().Any(ability => CanCastAbility(card, ability, boardZone));
        }

        public bool CanPlayCardWithDarkLordLogic(Card card, Player player)
        {
            bool isDarkLord = card.HasTrait("dark_lord");
            bool isVassal = card.HasTrait("vassal");

            // Check if player has a dark lord on the board
            bool hasDarkLord = player.cards_board.Any(c => c.HasTrait("dark_lord"));

            // Check if player has any non-vassal units
            bool hasNonVassalUnits = player.cards_board.Any(c => c.CardData.type == CardType.Character && !c.HasTrait("vassal"));

            // When playing a dark lord unit
            if (isDarkLord)
            {
                if (hasNonVassalUnits)
                {
                    return false; // Can't play dark lord if there are non-vassal units
                }
            }
            // When playing a non-dark lord and non-vassal unit
            else if (!isVassal && card.CardData.type == CardType.Character)
            {
                if (hasDarkLord)
                {
                    return false; // Can't play non-vassal unit if there's a dark lord
                }
            }
            // When playing a vassal
            else if (isVassal)
            {
                if (!hasDarkLord)
                {
                    return false; // Can't play vassal if there's no dark lord
                }
            }

            // If we've passed all checks, or it's not a relevant card type, return true
            return true;
        }

        //Check if a card is allowed to be played on slot
        public virtual bool CanPlayCard(Card card, Slot slot, bool skip_cost = false)
        {
            if (card == null)
                return false;

            Player player = GetPlayer(card.player_id);
            if (!skip_cost && (!player.CanPayMana(card) || !player.hasActions()))
                return false; //Cant pay mana or has no actions
            if (!player.HasCard(player.cards_hand, card) && !player.HasCard(player.cards_extra, card))
                return false; // Card not in hand

            // First, check the dark lord logic
            if (!CanPlayCardWithDarkLordLogic(card, player))
            {
                return false;
            }

            // Check Mythos for Miracles, Spells and Entities
            if (card.CardData.subtype == CardSubType.Miracle || card.CardData.subtype == CardSubType.Spell || card.CardData.subtype == CardSubType.Entity)
            {
                bool isGenericMythos = card.CardData.mythos == CardMythos.Generic;
                bool mythosDoesMatch = player.GetBoardMythos().Any(mythos => mythos == card.CardData.mythos);

                if(!isGenericMythos && !mythosDoesMatch)
                {
                    return false; // Player does not have correct mythos for summoning/casting.
                }
            }

            if (card.CardData.IsBoardCard())
            {
                if (!slot.IsValid() || IsCardOnSlot(slot))
                    return false;   //Slot already occupied
                if (Slot.GetP(card.player_id) != slot.p)
                    return false; //Cant play on opponent side
                return true;
            } 
            if (!card.CardData.IsCharacter())
            {
                // Logic that applies to all non-unit cards
                Player oplayer = GetPlayer(card.player_id == 0 ? 1 : 0);
                bool spell_limit = oplayer.cards_board.Any(card => card.HasStatus(StatusType.SpellLimit));
                if(active_player_non_unit_casts_this_turn >= 1 && spell_limit)
                {
                    return false;
                }

            }
            if (card.CardData.IsEquipment())
            {
                if (!slot.IsValid())
                    return false;

                Card target = GetSlotCard(slot);
                if (target == null || target.CardData.type != CardType.Character || target.player_id != card.player_id)
                    return false; //Target must be an allied character

                return true;
            }
            if (card.CardData.IsRequireTargetSpell())
            {
                return IsPlayTargetValid(card, slot); //Check play target on slot
            }
            return true;
        }

        //Check if a card is allowed to move to slot
        public virtual bool CanMoveCard(Card card, Slot slot, bool skip_cost = false)
        {
            if (card == null || !slot.IsValid())
                return false;

            if (!IsOnBoard(card))
                return false; //Only cards in play can move

            if (!card.CanMove(skip_cost))
                return false; //Card cant move

            if (Slot.GetP(card.player_id) != slot.p)
                return false; //Card played wrong side

            if (card.slot == slot)
                return false; //Cant move to same slot

            Card slot_card = GetSlotCard(slot);
            if (slot_card != null)
                return false; //Already a card there

            return true;
        }

        //Check if a card is allowed to attack a player
        public virtual bool CanAttackTarget(Card attacker, Player target, bool skip_cost = false)
        {
            if(attacker == null || target == null)
                return false;

            if (!attacker.CanAttack(skip_cost))
                return false; //Card cant attack

            if (attacker.player_id == target.player_id)
                return false; //Cant attack same player

            if (!IsOnBoard(attacker) || !attacker.CardData.IsCharacter())
                return false; //Cards not on board

            if (target.HasStatus(StatusType.Protected) && !attacker.HasStatus(StatusType.Flying))
                return false; //Protected by taunt

            if (target.GetOccupiedSlots().Count > 0)
                return false; //Cannot attack player that has units on the board

            return true;
        }

        //Check if a card is allowed to attack another one
        public virtual bool CanAttackTarget(Card attacker, Card target, bool skip_cost = false)
        {
            if (attacker == null || target == null)
                return false;

            if (!attacker.CanAttack(skip_cost))
                return false; //Card cant attack

            if (attacker.player_id == target.player_id)
                return false; //Cant attack same player

            if (!IsOnBoard(attacker) || !IsOnBoard(target))
                return false; //Cards not on board

            if (!attacker.CardData.IsCharacter() || !target.CardData.IsBoardCard())
                return false; //Only character can attack

            if (target.HasStatus(StatusType.Stealth))
                return false; //Stealth cant be attacked

            if (target.HasStatus(StatusType.Protected) && !attacker.HasStatus(StatusType.Flying))
                return false; //Protected by adjacent card

            return true;
        }

        public virtual bool CanCastAbility(Card card, AbilityData ability, int boardZone = 0)
        {
            bool shouldCheckCardActions = !ability.charge_player_action && !ability.free_action;

            if (ability == null || card == null || !card.CanDoActivatedAbilities(shouldCheckCardActions))
                return false; //This card cant cast

            if (ability.trigger != AbilityTrigger.Activate)
                return false; //Not an activated ability

            Player player = GetPlayer(card.player_id);

            if (shouldCheckCardActions)
            {
                if (!card.CanPayAbility(ability))
                    return false; //Cant pay for ability
            } else if (ability.charge_player_action)
            {
                if (!player.hasActions())
                    return false; //No player actions
            } 

            if (!ability.AreTriggerConditionsMet(this, card))
                return false; //Conditions not met

            if (boardZone == 1 && !ability.fromBoard)
                return false; // Cannot be played from board.

            if (boardZone == 2 && !ability.fromDiscard)
                return false; // Cannot be played from discard.

            if (boardZone == 3 && !ability.fromExtraDeck)
                return false; // Cannot be played from extra deck.

            return true;
        }

        //For choice selector
        public virtual bool CanSelectAbility(Card card, AbilityData ability, int boardZone = 0)
        {
            if (ability == null || card == null || !card.CanDoAbilities())
                return false; //This card cant cast

            Player player = GetPlayer(card.player_id);
            if (!player.CanPayAbility(card, ability))
                return false; //Cant pay for ability

            if (!ability.AreTriggerConditionsMet(this, card))
                return false; //Conditions not met

            if (boardZone == 1 && !ability.fromBoard)
                return false; // Cannot be played from board.

            if (boardZone == 2 && !ability.fromDiscard)
                return false; // Cannot be played from discard.

            if (boardZone == 3 && !ability.fromExtraDeck)
                return false; // Cannot be played from extra deck.

            return true;
        }

        //Check if Player play target is valid, play target is the target when a spell requires to drag directly onto another card
        public virtual bool IsPlayTargetValid(Card caster, Player target)
        {
            if (caster == null || target == null)
                return false;

            foreach (AbilityData ability in caster.GetAbilities())
            {
                if (ability && ability.trigger == AbilityTrigger.OnPlay && ability.target == AbilityTarget.PlayTarget)
                {
                    if (!ability.CanTarget(this, caster, target))
                        return false;
                }
            }
            return true;
        }

        //Check if Card play target is valid, play target is the target when a spell requires to drag directly onto another card
        public virtual bool IsPlayTargetValid(Card caster, Card target)
        {
            if (caster == null || target == null)
                return false;

            foreach (AbilityData ability in caster.GetAbilities())
            {
                if (ability && ability.trigger == AbilityTrigger.OnPlay && ability.target == AbilityTarget.PlayTarget)
                {
                    if (!ability.CanTarget(this, caster, target))
                        return false;
                }
            }
            return true;
        }

        //Check if Slot play target is valid, play target is the target when a spell requires to drag directly onto another card
        public virtual bool IsPlayTargetValid(Card caster, Slot target)
        {
            if (caster == null)
                return false;

            if (target.IsPlayerSlot())
                return IsPlayTargetValid(caster, GetPlayer(target.p)); //Slot 0,0, means we are targeting a player

            Card slot_card = GetSlotCard(target);
            if (slot_card != null)
                return IsPlayTargetValid(caster, slot_card); //Slot has card, check play target on that card

            foreach (AbilityData ability in caster.GetAbilities())
            {
                if (ability && ability.trigger == AbilityTrigger.OnPlay && ability.target == AbilityTarget.PlayTarget)
                {
                    if (!ability.CanTarget(this, caster, target))
                        return false;
                }
            }
            return true;
        }

        public Player GetPlayer(int id)
        {
            if (id >= 0 && id < players.Length)
                return players[id];
            return null;
        }

        public Player GetActivePlayer()
        {
            return GetPlayer(current_player);
        }

        public Player GetOpponentPlayer(int id)
        {
            int oid = id == 0 ? 1 : 0;
            return GetPlayer(oid);
        }

        public Card GetCard(string card_uid)
        {
            foreach (Player player in players)
            {
                Card acard = player.GetCard(card_uid);
                if (acard != null)
                    return acard;
            }
            return null;
        }

        public Card GetBoardCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_board)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetEquipCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_equip)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetHandCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_hand)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetDeckCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_deck)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetExtraDeckCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_extra)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetDiscardCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_discard)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetSecretCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_secret)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetTempCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_temp)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetSlotCard(Slot slot)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_board)
                {
                    if (card != null && card.slot == slot)
                        return card;
                }
            }
            return null;
        }
        
        public virtual Player GetRandomPlayer(System.Random rand)
        {
            Player player = GetPlayer(rand.NextDouble() < 0.5 ? 1 : 0);
            return player;
        }

        public virtual Card GetRandomBoardCard(System.Random rand)
        {
            Player player = GetRandomPlayer(rand);
            return player.GetRandomCard(player.cards_board, rand);
        }

        public virtual Slot GetRandomSlot(System.Random rand)
        {
            Player player = GetRandomPlayer(rand);
            return player.GetRandomSlot(rand);
        }

        public bool IsInHand(Card card)
        {
            return card != null && GetHandCard(card.uid) != null;
        }

        public bool IsOnBoard(Card card)
        {
            return card != null && GetBoardCard(card.uid) != null;
        }

        public bool IsEquipped(Card card)
        {
            return card != null && GetEquipCard(card.uid) != null;
        }

        public bool IsInDeck(Card card)
        {
            return card != null && GetDeckCard(card.uid) != null;
        }

        public bool IsInExtraDeck(Card card)
        {
            return card != null && GetExtraDeckCard(card.uid) != null;
        }

        public bool IsInDiscard(Card card)
        {
            return card != null && GetDiscardCard(card.uid) != null;
        }

        public bool IsInSecret(Card card)
        {
            return card != null && GetSecretCard(card.uid) != null;
        }

        public bool IsInTemp(Card card)
        {
            return card != null && GetTempCard(card.uid) != null;
        }

        public bool IsCardOnSlot(Slot slot)
        {
            return GetSlotCard(slot) != null;
        }

        public bool HasStarted()
        {
            return state != GameState.Connecting;
        }

        public bool HasEnded()
        {
            return state == GameState.GameEnded;
        }

        //Same as clone, but also instantiates the variable (much slower)
        public static Game CloneNew(Game source)
        {
            Game game = new Game();
            Clone(source, game);
            return game;
        }

        //Clone all variables into another var, used mostly by the AI when building a prediction tree
        public static void Clone(Game source, Game dest)
        {
            dest.game_uid = source.game_uid;
            dest.settings = source.settings;

            dest.first_player = source.first_player;
            dest.current_player = source.current_player;
            dest.turn_count = source.turn_count;
            dest.turn_timer = source.turn_timer;
            dest.state = source.state;
            dest.phase = source.phase;

            if (dest.players == null)
            {
                dest.players = new Player[source.players.Length];
                for(int i=0; i< source.players.Length; i++)
                    dest.players[i] = new Player(i);
            }

            for (int i = 0; i < source.players.Length; i++)
                Player.Clone(source.players[i], dest.players[i]);

            dest.selector = source.selector;
            dest.selector_player_id = source.selector_player_id;
            dest.selector_caster_uid = source.selector_caster_uid;
            dest.selector_ability_id = source.selector_ability_id;

            dest.last_destroyed = source.last_destroyed;
            dest.last_played = source.last_played;
            dest.last_target = source.last_target;
            dest.last_summoned = source.last_summoned;
            dest.ability_triggerer = source.ability_triggerer;
            dest.rolled_value = source.rolled_value;

            CloneHash(source.ability_played, dest.ability_played);
            CloneHash(source.cards_attacked, dest.cards_attacked);
        }

        public static void CloneHash(HashSet<string> source, HashSet<string> dest)
        {
            dest.Clear();
            foreach (string str in source)
                dest.Add(str);
        }
    }

    [System.Serializable]
    public enum GameState
    {
        Connecting = 0, //Players are not connected
        Play = 20,      //Game is being played
        GameEnded = 99,
    }

    [System.Serializable]
    public enum GamePhase
    {
        None = 0,
        StartTurn = 10, //Start of turn resolution
        Main = 20,      //Main play phase
        EndTurn = 30,   //End of turn resolutions
    }

    [System.Serializable]
    public enum SelectorType
    {
        None = 0,
        SelectTarget = 10,
        SelectorCard = 20,
        SelectorChoice = 30,
    }
}