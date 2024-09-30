using System;
using System.Collections.Generic;
using System.Linq;
using TcgEngine.AI;
using TcgEngine.Client;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Profiling;
using static UnityEngine.GraphicsBuffer;

namespace TcgEngine.Gameplay
{
    public enum AIMessageEvent
    {
        FirstEncounter,
        SubsequentEncounterFirst,
        SubsequentEncounterSecond,
        AITurnStart,
        PlayerTurnStart,
        AITurnEnd,
        PlayerTurnEnd,
        AISummonPowerfulUnit,
        PlayerSummonPowerfulUnit,
        AIFusionSummon,
        PlayerFusionSummon,
        AIDirectAttack,
        PlayerDirectAttack,
        AIActivatePowerfulSpell,
        PlayerActivatePowerfulSpell,
        AIActivatePowerfulAbility,
        PlayerActivatePowerfulAbility,
        AILowHP,
        PlayerLowHP,
        AILastCard,
        PlayerLastCard,
        AIDestroyKeyUnit,
        PlayerDestroyKeyUnit,
        AIDestroyKeyRelic,
        PlayerDestroyKeyRelic,
        AICounterPlayerStrategy,
        PlayerCounterAIStrategy,
        AIPlayingEquipment,
        PlayerPlayingEquipment,
        AISettingWard,
        PlayerSettingWard,
        PlayerWinning,
        AIWinning,
        MirrorMatch,
        Surprise,
        Frustration,
        Excitement,
        Contemplation,
        IdleComments
    }

    public enum AICharacter
    {
        Generic = 0,
        ClaraFischer = 1,
        JamalHarris = 2,
        LydiaKorovin = 3,
        MateoRamirez = 4,
        MikaelRodriguez = 5,
        RachelMorgan = 6,
        SarahRegnitz = 7,
        SebastionVonAdler = 8,
        SophiaWeber = 9,
        TylerJohnson = 10
    }



    /// <summary>
    /// Execute and resolves game rules and logic
    /// </summary>

    public class GameLogic
    {
        public UnityAction onGameStart;
        public UnityAction<Player> onGameEnd;          //Winner

        public UnityAction onTurnStart;
        public UnityAction onTurnPlay;
        public UnityAction onTurnEnd;

        public UnityAction<Card, Slot> onCardPlayed;      
        public UnityAction<Card, Slot> onCardSummoned;
        public UnityAction<Card, Slot> onCardMoved;
        public UnityAction<Card> onCardTransformed;
        public UnityAction<Card> onCardDiscarded;
        public UnityAction<int> onCardDrawn;
        public UnityAction<int> onRollValue;

        public UnityAction<AbilityData, Card> onAbilityStart;        
        public UnityAction<AbilityData, Card, Card> onAbilityTargetCard;  //Ability, Caster, Target
        public UnityAction<AbilityData, Card, Player> onAbilityTargetPlayer;
        public UnityAction<AbilityData, Card, Slot> onAbilityTargetSlot;
        public UnityAction<AbilityData, Card> onAbilityEnd;

        public UnityAction<Card, Card> onAttackStart;  //Attacker, Defender
        public UnityAction<Card, Card> onAttackEnd;     //Attacker, Defender
        public UnityAction<Card, Player> onAttackPlayerStart;
        public UnityAction<Card, Player> onAttackPlayerEnd;

        public UnityAction<Card, Card> onSecretTrigger;    //Secret, Triggerer
        public UnityAction<Card, Card> onSecretResolve;    //Secret, Triggerer

        public UnityAction onRefresh;

        private Game game_data;

        private ResolveQueue resolve_queue;
        private bool is_ai_predict = false;
        private bool has_ai_player = false;
        private AICharacter ai_character;
        private DateTime last_message_display_time = DateTime.MinValue;
        private const int MIN_SECONDS_BETWEEN_MESSAGES = 30;

        private System.Random random = new System.Random();

        private ListSwap<Card> card_array = new ListSwap<Card>();
        private ListSwap<Player> player_array = new ListSwap<Player>();
        private ListSwap<Slot> slot_array = new ListSwap<Slot>();
        private ListSwap<CardData> card_data_array = new ListSwap<CardData>();
        private List<Card> cards_to_clear = new List<Card>();

        public GameLogic(bool is_ai)
        {
            //is_instant ignores all gameplay delays and process everything immediately, needed for AI prediction
            resolve_queue = new ResolveQueue(null, is_ai);
            is_ai_predict = is_ai;
        }

        public GameLogic(Game game)
        {
            game_data = game;
            resolve_queue = new ResolveQueue(game, false);
        }

        public virtual void SetData(Game game)
        {
            game_data = game;
            resolve_queue.SetData(game);
        }

        public virtual void Update(float delta)
        {
            resolve_queue.Update(delta);
        }

        //----- Turn Phases ----------

        public virtual void StartGame()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            //Choose first player
            game_data.state = GameState.Play;
            game_data.first_player = random.NextDouble() < 0.5 ? 0 : 1;
            game_data.current_player = game_data.first_player;
            game_data.turn_count = 1;

            //Adventure settings
            LevelData level = game_data.settings.GetLevel();
            if (level != null)
            {
                if (level != null && level.first_player == LevelFirst.Player)
                    game_data.first_player = 0;
                if (level != null && level.first_player == LevelFirst.AI)
                    game_data.first_player = 1;
                game_data.current_player = game_data.first_player;
            }

            //TODO Add setting for game difficulty in adventure mode. For now will default to a harder difficulty setting.
            game_data.settings.game_difficulty = GameDifficulty.Challenging;

            //Init each player
            foreach (Player player in game_data.players)
            {

                //Hp / mana
                player.hp_max = GameplayData.Get().hp_start;
                player.hp = player.hp_max;
                player.mana_max = GameplayData.Get().mana_start;
                player.mana = player.mana_max;

                if (player.is_ai)
                {
                    if(game_data.settings.game_difficulty == GameDifficulty.Challenging)
                    {
                        player.hp_max += 10;
                        player.hp = player.hp_max;
                        player.mana_max += 10;
                        player.mana = player.mana_max;
                    }
                }

                // Set AI Profile Details
                if(level != null)
                {
                    SetupAICharacterLogic(player, level.opponentName);
                } else
                {
                    SetupAICharacterLogic(player);
                }
                

                //Move extra deck cards

                // Find all 'Fusion' cards in the deck
                List<int> fusionIndices = new List<int>();
                for (int i = 0; i < player.cards_deck.Count; i++)
                {
                    if (player.cards_deck[i].CardData.subtype == CardSubType.Fusion)
                    {
                        fusionIndices.Add(i);
                    }
                }

                // Iterate through the indices in reverse order to avoid index shifting issues
                for (int i = fusionIndices.Count - 1; i >= 0; i--)
                {
                    int fusionIndex = fusionIndices[i];
                    player.cards_extra.Add(player.cards_deck[fusionIndex]);
                    player.cards_deck.RemoveAt(fusionIndex);
                }

                //Add a Summoner to hand
                int summonerIndex = player.cards_deck.FindIndex(card => card.CardData.subtype == CardSubType.Summoner);
                if (summonerIndex != -1)
                {
                    player.cards_hand.Add(player.cards_deck[summonerIndex]);
                    player.cards_deck.RemoveAt(summonerIndex);
                }

                //Add another unit to hand (Summoner or Entity)
                int unitIndex = player.cards_deck.FindIndex(card => card.CardData.subtype == CardSubType.Summoner || card.CardData.subtype == CardSubType.Entity);
                if (unitIndex != -1)
                {
                    player.cards_hand.Add(player.cards_deck[unitIndex]);
                    player.cards_deck.RemoveAt(unitIndex);
                }

                //Add a Spell or Skill card to hand
                int spellSkillIndex = player.cards_deck.FindIndex(card => card.CardData.subtype == CardSubType.Spell || card.CardData.subtype == CardSubType.Skill);
                if (spellSkillIndex != -1)
                {
                    player.cards_hand.Add(player.cards_deck[spellSkillIndex]);
                    player.cards_deck.RemoveAt(spellSkillIndex);
                }

                //Add an Item card to hand
                int itemIndex = player.cards_deck.FindIndex(card => card.CardData.subtype == CardSubType.Item);
                if (itemIndex != -1)
                {
                    player.cards_hand.Add(player.cards_deck[itemIndex]);
                    player.cards_deck.RemoveAt(itemIndex);
                }

                //Add a Miracle card to hand for second player only
                bool is_random = level == null || level.first_player == LevelFirst.Random;

                if (is_random && player.player_id != game_data.first_player && GameplayData.Get().second_bonus != null)
                {
                    
                    int miracleIndex = player.cards_deck.FindIndex(card => card.CardData.subtype == CardSubType.Miracle);
                    if (miracleIndex != -1)
                    {
                        player.cards_hand.Add(player.cards_deck[miracleIndex]);
                        player.cards_deck.RemoveAt(miracleIndex);
                    }
                }

                if (player.is_ai)
                {
                    if (game_data.settings.game_difficulty == GameDifficulty.Challenging)
                    {
                        int miracleIndex = player.cards_deck.FindIndex(card => card.CardData.subtype == CardSubType.Miracle);
                        if (miracleIndex != -1)
                        {
                            player.cards_hand.Add(player.cards_deck[miracleIndex]);
                            player.cards_deck.RemoveAt(miracleIndex);
                        }
                    }
                }
            }

            //Start state
            RefreshData();
            onGameStart?.Invoke();

            DisplayAIMessageEvent(AIMessageEvent.FirstEncounter);

            StartTurn();
        }
		
        public virtual void StartTurn()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            ClearTurnData();
            game_data.phase = GamePhase.StartTurn;
            onTurnStart?.Invoke();
            RefreshData();

            Player player = game_data.GetActivePlayer();

            if (player.is_ai)
                DisplayAIMessageEvent(AIMessageEvent.AITurnStart);
            else
                DisplayAIMessageEvent(AIMessageEvent.PlayerTurnStart);

            //Cards draw
            DrawCard(player, GameplayData.Get().cards_per_turn, player.is_ai);

            //Mana 
            player.mana_max += GameplayData.Get().mana_per_turn;
            player.mana_max = Mathf.Min(player.mana_max, GameplayData.Get().mana_max);

            //Turn timer and history
            game_data.turn_timer = GameplayData.Get().turn_duration;
            player.history_list.Clear();

            if (player.hero != null)
                player.hero.Refresh();

            //Refresh Cards and Status Effects
            for (int i = player.cards_board.Count - 1; i >= 0; i--)
            {
                Card card = player.cards_board[i];
                card.Refresh();
                card.AdjustStatusesPassively(true);

                foreach (StatusType status in Enum.GetValues(typeof(StatusType)))
                {
                    if (card.HasStatus(status))
                    {
                        int damage = CalculateDOTDamage(card, status);
                        if (damage > 0)
                        {
                            DamageCard(card, damage);
                        }
                    }
                }
            }

            //Ongoing Abilities
            UpdateOngoing();

            //StartTurn Abilities
            TriggerPlayerCardsAbilityType(player, AbilityTrigger.StartOfTurn);
            TriggerPlayerSecrets(player, AbilityTrigger.StartOfTurn);

            resolve_queue.AddCallback(StartMainPhase);
            resolve_queue.ResolveAll(0.2f);
        }

        public virtual void StartNextTurn()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            game_data.current_player = (game_data.current_player + 1) % game_data.settings.nb_players;
            game_data.selector_player_id = game_data.current_player;
            
            if (game_data.current_player == game_data.first_player)
                game_data.turn_count++;

            CheckForWinner();
            StartTurn();
        }

        public virtual void StartMainPhase()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            game_data.phase = GamePhase.Main;
            onTurnPlay?.Invoke();
            RefreshData();
        }

        public virtual void EndTurn()
        {
            if (game_data.state == GameState.GameEnded)
                return;
            if (game_data.phase != GamePhase.Main)
                return;


            game_data.selector = SelectorType.None;
            game_data.phase = GamePhase.EndTurn;

            //Reduce status effects with duration
            foreach (Player aplayer in game_data.players)
            {
                foreach (Card card in aplayer.cards_board)
                    card.ReduceStatusDurations();
                foreach (Card card in aplayer.cards_equip)
                    card.ReduceStatusDurations();
            }

            //End of turn abilities
            Player player = game_data.GetActivePlayer();
            TriggerPlayerCardsAbilityType(player, AbilityTrigger.EndOfTurn);

            if (player.hp <= 5) // Assuming 5 or less HP is "low"
            {
                if (!player.is_ai)
                    DisplayAIMessageEvent(AIMessageEvent.PlayerLowHP);
                else
                    DisplayAIMessageEvent(AIMessageEvent.AILowHP);
            }

            if (player.is_ai)
                DisplayAIMessageEvent(AIMessageEvent.AITurnEnd);
            else
                DisplayAIMessageEvent(AIMessageEvent.PlayerTurnEnd);


            onTurnEnd?.Invoke();
            RefreshData();

            resolve_queue.AddCallback(StartNextTurn);
            resolve_queue.ResolveAll(0.2f);
        }

        //End game with winner
        public virtual void EndGame(int winner)
        {
            if (game_data.state != GameState.GameEnded)
            {
                game_data.state = GameState.GameEnded;
                game_data.phase = GamePhase.None;
                game_data.selector = SelectorType.None;
                game_data.current_player = winner; //Winner player
                resolve_queue.Clear();
                Player player = game_data.GetPlayer(winner);

                if (player.is_ai)
                    DisplayAIMessageEvent(AIMessageEvent.AIWinning);
                else
                    DisplayAIMessageEvent(AIMessageEvent.PlayerWinning);

                onGameEnd?.Invoke(player);
                RefreshData();
            }
        }

        //Progress to the next step/phase 
        public virtual void NextStep()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            CancelSelection();

            //Add to resolve queue in case its still resolving
            resolve_queue.AddCallback(EndTurn);
            resolve_queue.ResolveAll();
        }

        //Check if a player is winning the game, if so end the game
        //Change or edit this function for a new win condition
        protected virtual void CheckForWinner()
        {
            int count_alive = 0;
            Player alive = null;
            foreach (Player player in game_data.players)
            {
                if (!player.IsDead())
                {
                    alive = player;
                    count_alive++;
                }
            }

            if (count_alive == 0)
            {
                EndGame(-1); //Everyone is dead, Draw
            }
            else if (count_alive == 1)
            {
                EndGame(alive.player_id); //Player win
            }
        }

        protected virtual void ClearTurnData()
        {
            game_data.selector = SelectorType.None;
            resolve_queue.Clear();
            card_array.Clear();
            player_array.Clear();
            slot_array.Clear();
            card_data_array.Clear();
            game_data.last_played = null;
            game_data.last_destroyed = null;
            game_data.last_target = null;
            game_data.last_summoned = null;
            game_data.ability_triggerer = null;
            game_data.ability_played.Clear();
            game_data.cards_attacked.Clear();
            game_data.active_player_non_unit_casts_this_turn = 0;

            foreach (Player aplayer in game_data.players)
            {
                aplayer.InitPlayerActions();
            }
        }

        //--- Setup ------

        //Set deck using a Deck in Resources
        public virtual void SetPlayerDeck(Player player, DeckData deck)
        {
            player.cards_all.Clear();
            player.cards_deck.Clear();
            player.deck = deck.id;
            player.hero = null;

            VariantData variant = VariantData.GetDefault();
            if (deck.hero != null)
            {
                player.hero = Card.Create(deck.hero, variant, player);
            }

            foreach (CardData card in deck.cards)
            {
                if (card != null)
                {
                    Card acard = Card.Create(card, variant, player);
                    player.cards_deck.Add(acard);
                }
            }

            DeckPuzzleData puzzle = deck as DeckPuzzleData;

            //Board cards
            if (puzzle != null)
            {
                foreach (DeckCardSlot card in puzzle.board_cards)
                {
                    Card acard = Card.Create(card.card, variant, player);
                    acard.slot = new Slot(card.slot, Slot.GetP(player.player_id));
                    player.cards_board.Add(acard);
                }
            }

            //Shuffle deck
            if(puzzle == null || !puzzle.dont_shuffle_deck)
                ShuffleDeck(player.cards_deck);
        }

        //Set deck using custom deck in save file or database
        public virtual void SetPlayerDeck(Player player, UserDeckData deck)
        {
            player.cards_all.Clear();
            player.cards_deck.Clear();
            player.deck = deck.tid;
            player.hero = null;

            if (deck.hero != null)
            {
                CardData hdata = CardData.Get(deck.hero.tid);
                VariantData hvariant = VariantData.Get(deck.hero.variant);
                if(hdata != null && hvariant != null)
                    player.hero = Card.Create(hdata, hvariant, player);
            }

            foreach (UserCardData card in deck.cards)
            {
                CardData icard = CardData.Get(card.tid);
                VariantData variant = VariantData.Get(card.variant);
                if (icard != null && variant != null)
                {
                    for (int i = 0; i < card.quantity; i++)
                    {
                        Card acard = Card.Create(icard, variant, player);
                        player.cards_deck.Add(acard);
                    }
                }
            }

            //Shuffle deck
            ShuffleDeck(player.cards_deck);
        }

        //---- Gameplay Actions --------------

        public virtual void PlayCard(Card card, Slot slot, bool skip_cost = false)
        {
            if (game_data.CanPlayCard(card, slot, skip_cost))
            {
                TriggerSecrets(AbilityTrigger.OnBeforePlayOther, card); //After playing card
                TriggerOtherCardsAbilityType(AbilityTrigger.OnBeforePlayOther, card);

                TriggerCardAbilityType(AbilityTrigger.OnBeforePlaySelf, card);

                resolve_queue.ResolveAll(0.3f);

                Player player = game_data.GetPlayer(card.player_id);
                
                //Cost
                if (!skip_cost)
                    player.PayMana(card);

                if (card.HasStatus(StatusType.CounteredToHand))
                {
                    card.RemoveStatus(StatusType.CounteredToHand);
                    resolve_queue.ResolveAll(0.3f);
                    return;
                }

                //Play card
                player.RemoveCardFromAllGroups(card);

                if (card.HasStatus(StatusType.Countered))
                {
                    card.RemoveStatus(StatusType.Countered);
                    player.cards_discard.Add(card);
                    card.slot = slot;
                    resolve_queue.ResolveAll(0.3f);
                    return;
                }

                if (card.HasStatus(StatusType.CounteredToDeck))
                {
                    card.RemoveStatus(StatusType.CounteredToDeck);
                    player.cards_deck.Add(card);
                    card.slot = slot;
                    resolve_queue.ResolveAll(0.3f);
                    return;
                }

                //Add to board
                CardData icard = card.CardData;
                if (icard.IsBoardCard())
                {
                    player.cards_board.Add(card);
                    card.slot = slot;
                    card.exhausted = false;
                    if (game_data.turn_count > 1)
                    {
                        card.totalActions = 1;
                        card.availableHalfActions = 1;
                    }
                    else
                    {
                        card.totalActions = 0;
                        card.availableHalfActions = 0;
                    }
                    card.availableActions = 0;
                }
                else if (icard.IsEquipment())
                {
                    Card bearer = game_data.GetSlotCard(slot);
                    EquipCard(bearer, card);
                    card.exhausted = false;
                }
                else if (icard.IsSecret())
                {
                    player.cards_secret.Add(card);
                }
                else
                {
                    player.cards_discard.Add(card);
                    card.slot = slot; //Save slot in case spell has PlayTarget
                }

                if (!icard.IsCharacter())
                {
                    game_data.active_player_non_unit_casts_this_turn += 1;
                }

                if (icard.IsBoardCard() && card.HasTrait("lord"))
                {
                    if (!player.is_ai)
                        DisplayAIMessageEvent(AIMessageEvent.PlayerSummonPowerfulUnit);
                    else
                        DisplayAIMessageEvent(AIMessageEvent.AISummonPowerfulUnit);
                }
                else if (icard.IsEquipment())
                {
                    if (!player.is_ai)
                        DisplayAIMessageEvent(AIMessageEvent.PlayerPlayingEquipment);
                    else
                        DisplayAIMessageEvent(AIMessageEvent.AIPlayingEquipment);
                }
                else if (icard.IsSecret())
                {
                    if (!player.is_ai)
                        DisplayAIMessageEvent(AIMessageEvent.PlayerSettingWard);
                    else
                        DisplayAIMessageEvent(AIMessageEvent.AISettingWard);
                }

                if (skip_cost && icard.IsCharacter())
                {
                    // Non-fusion special summons do not use actions, but fusions and normal summons still do.
                    if(icard.subtype == CardSubType.Fusion)
                        player.useAction(false);
                } else
                {
                    if (icard.IsCharacter() || icard.subtype == CardSubType.Miracle)
                    {
                        player.useAction(false);
                    }
                    else
                    {
                        player.useAction();
                    }
                }


                //History
                if (!is_ai_predict && !icard.IsSecret())
                    player.AddHistory(GameAction.PlayCard, card);

                //Update ongoing effects
                game_data.last_played = card.uid;
                UpdateOngoing();

                //Trigger abilities
                TriggerSecrets(AbilityTrigger.OnPlayOther, card); //After playing card
                TriggerCardAbilityType(AbilityTrigger.OnPlay, card);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnPlayOther, card);
                
                RefreshData();

                onCardPlayed?.Invoke(card, slot);
                resolve_queue.ResolveAll(0.3f);
            }
        }

        public virtual void MoveCard(Card card, Slot slot, bool skip_cost = false)
        {
            if (game_data.CanMoveCard(card, slot, skip_cost))
            {
                card.slot = slot;

                //Moving doesn't really have any effect in demo so can be done indefinitely
                //if(!skip_cost)
                //card.exhausted = true;
                //card.RemoveStatus(StatusEffect.Stealth);
                //player.AddHistory(GameAction.Move, card);

                //Also move the equipment
                Card equip = game_data.GetEquipCard(card.equipped_uid);
                if (equip != null)
                    equip.slot = slot;

                UpdateOngoing();
                RefreshData();

                onCardMoved?.Invoke(card, slot);
                resolve_queue.ResolveAll(0.2f);
            }
        }

        public virtual void CastAbility(Card card, AbilityData iability)
        {
            if (game_data.CanCastAbility(card, iability))
            {
                Player player = game_data.GetPlayer(card.player_id);
                if (!is_ai_predict && iability.target != AbilityTarget.SelectTarget)
                    player.AddHistory(GameAction.CastAbility, card, iability);
                card.RemoveStatus(StatusType.Stealth);
                TriggerCardAbility(iability, card);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void AttackTarget(Card attacker, Card target, bool skip_cost = false)
        {
            if (game_data.CanAttackTarget(attacker, target, skip_cost))
            {

                Player player = game_data.GetPlayer(attacker.player_id);
                if(!is_ai_predict)
                    player.AddHistory(GameAction.Attack, attacker, target);

                //Trigger before attack abilities
                TriggerCardAbilityType(AbilityTrigger.OnBeforeAttack, attacker, target);
                TriggerCardAbilityType(AbilityTrigger.OnBeforeDefend, target, attacker);
                TriggerSecrets(AbilityTrigger.OnBeforeAttack, attacker);
                TriggerSecrets(AbilityTrigger.OnBeforeDefend, target);

                //Resolve attack
                resolve_queue.AddAttack(attacker, target, ResolveAttack, skip_cost);
                resolve_queue.ResolveAll();
            }
        }

        protected virtual void ResolveAttack(Card attacker, Card target, bool skip_cost)
        {
            if (!game_data.IsOnBoard(attacker) || !game_data.IsOnBoard(target))
                return;

            onAttackStart?.Invoke(attacker, target);

            attacker.RemoveStatus(StatusType.Stealth);
            UpdateOngoing();

            resolve_queue.AddAttack(attacker, target, ResolveAttackHit, skip_cost);
            resolve_queue.ResolveAll(0.3f);
        }

        protected virtual void ResolveAttackHit(Card attacker, Card target, bool skip_cost)
        {
            //Count attack damage
            int datt1 = attacker.GetAttack();

            DamageCard(attacker, target, datt1, attacker.CardData.primaryElement);

            //Save attack and exhaust
            if (!skip_cost)
                ExhaustBattle(attacker);

            //Recalculate bonus
            UpdateOngoing();

            //Abilities
            bool att_board = game_data.IsOnBoard(attacker);
            bool def_board = game_data.IsOnBoard(target);
            if (att_board)
                TriggerCardAbilityType(AbilityTrigger.OnAfterAttack, attacker, target);
            if (def_board)
                TriggerCardAbilityType(AbilityTrigger.OnAfterDefend, target, attacker);
            if (att_board)
                TriggerSecrets(AbilityTrigger.OnAfterAttack, attacker);
            if (def_board)
                TriggerSecrets(AbilityTrigger.OnAfterDefend, target);


            RefreshData();

            onAttackEnd?.Invoke(attacker, target);

            CheckForWinner();

            resolve_queue.ResolveAll(0.2f);
        }

        public virtual void AttackPlayer(Card attacker, Player target, bool skip_cost = false)
        {
            if (attacker == null || target == null)
                return;

            if (!game_data.CanAttackTarget(attacker, target, skip_cost))
                return;

            Player player = game_data.GetPlayer(attacker.player_id);
            if(!is_ai_predict)
                player.AddHistory(GameAction.AttackPlayer, attacker, target);

            if (player.is_ai)
            {
                DisplayAIMessageEvent(AIMessageEvent.AIDirectAttack);
            } else
            {
                DisplayAIMessageEvent(AIMessageEvent.PlayerDirectAttack);
            }
            

            //Resolve abilities
            TriggerSecrets(AbilityTrigger.OnBeforeAttack, attacker);
            TriggerCardAbilityType(AbilityTrigger.OnBeforeAttack, attacker, target);

            //Resolve attack
            resolve_queue.AddAttack(attacker, target, ResolveAttackPlayer, skip_cost);
            resolve_queue.ResolveAll();
        }

        protected virtual void ResolveAttackPlayer(Card attacker, Player target, bool skip_cost)
        {
            if (!game_data.IsOnBoard(attacker))
                return;

            onAttackPlayerStart?.Invoke(attacker, target);

            attacker.RemoveStatus(StatusType.Stealth);
            UpdateOngoing();

            resolve_queue.AddAttack(attacker, target, ResolveAttackPlayerHit, skip_cost);
            resolve_queue.ResolveAll(0.3f);
        }

        protected virtual void ResolveAttackPlayerHit(Card attacker, Player target, bool skip_cost)
        {
            DamagePlayer(attacker, target, attacker.GetAttack());

            //Save attack and exhaust
            if (!skip_cost)
                ExhaustBattle(attacker);

            //Recalculate bonus
            UpdateOngoing();

            if (game_data.IsOnBoard(attacker))
                TriggerCardAbilityType(AbilityTrigger.OnAfterAttack, attacker, target);

            TriggerSecrets(AbilityTrigger.OnAfterAttack, attacker);

            onAttackPlayerEnd?.Invoke(attacker, target);
            RefreshData();
            CheckForWinner();

            resolve_queue.ResolveAll(0.2f);
        }

        //Exhaust after battle
        public virtual void ExhaustBattle(Card attacker)
        {
            bool attacked_before = game_data.cards_attacked.Contains(attacker.uid);
            game_data.cards_attacked.Add(attacker.uid);
            if (attacker.HasStatus(StatusType.Fury))
            {
                GrantBonusAction(attacker);
            }
            ReduceActionValue(attacker);
        }

        public virtual void ReduceActionValue(Card attacker)
        {
            if (attacker.availableActions > 0)
            {
                attacker.availableActions--;
            }
            else if (attacker.availableHalfActions > 0)
            {
                attacker.availableHalfActions--;
            }
            attacker.totalActions = attacker.availableHalfActions + attacker.availableActions;
        }

        public virtual void GrantBonusAction(Card attacker)
        {
            if(attacker.availableActions > 0)
            {
                attacker.availableHalfActions = 1;
            }
            attacker.totalActions = attacker.availableHalfActions + attacker.availableActions;
        }

        public virtual void InitUnitActions(Card attacker)
        {
            int actions = 0;

            if (attacker.agility > 0)
            {
                actions = 1;
            }

            attacker.availableActions = actions;
            attacker.availableHalfActions = 0;
            attacker.totalActions = actions;
        }

        //Redirect attack to a new target
        public virtual void RedirectAttack(Card attacker, Card new_target)
        {
            foreach (AttackQueueElement att in resolve_queue.GetAttackQueue())
            {
                if (att.attacker.uid == attacker.uid)
                {
                    att.target = new_target;
                    att.ptarget = null;
                    att.callback = ResolveAttack;
                    att.pcallback = null;
                }
            }
        }

        public virtual void RedirectAttack(Card attacker, Player new_target)
        {
            foreach (AttackQueueElement att in resolve_queue.GetAttackQueue())
            {
                if (att.attacker.uid == attacker.uid)
                {
                    att.ptarget = new_target;
                    att.target = null;
                    att.pcallback = ResolveAttackPlayer;
                    att.callback = null;
                }
            }
        }

        public virtual void ShuffleDeck(List<Card> cards)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                Card temp = cards[i];
                int randomIndex = random.Next(i, cards.Count);
                cards[i] = cards[randomIndex];
                cards[randomIndex] = temp;
            }
        }

        public virtual void DrawCard(Player player, int nb = 1, bool destinyDraw = false)
        {
            for (int i = 0; i < nb; i++)
            {
                if (destinyDraw)
                {
                    int emptySlots = player.GetEmptySlots().Count();
                    bool hasEmptySlot = emptySlots > 0;
                    bool hasHandUnit = player.HasHandUnit();
                    bool requiresUnit = hasEmptySlot && !hasHandUnit;
                    bool hasSmallBoard = emptySlots > 3;

                    if (hasSmallBoard || requiresUnit)
                    {
                        // Draw a unit card
                        if (player.mana < 10)
                        {
                            // Try to draw a summoner first
                            int summonerIndex = player.cards_deck.FindIndex(card => card.CardData.subtype == CardSubType.Summoner);
                            if (summonerIndex != -1)
                            {
                                DrawSpecificCard(player, summonerIndex);
                            }
                            else
                            {
                                // Fall back to any unit
                                int unitIndex = player.cards_deck.FindIndex(card => card.CardData.subtype == CardSubType.Summoner || card.CardData.subtype == CardSubType.Entity);
                                if (unitIndex != -1)
                                {
                                    DrawSpecificCard(player, unitIndex);
                                }
                                else
                                {
                                    // Fall back to utility logic if no units available
                                    DrawUtilityCard(player);
                                }
                            }
                        }
                        else
                        {
                            // Randomly draw a unit or summoner
                            List<int> unitIndices = player.cards_deck
                                .Select((card, index) => new { Card = card, Index = index })
                                .Where(x => x.Card.CardData.subtype == CardSubType.Summoner || x.Card.CardData.subtype == CardSubType.Entity)
                                .Select(x => x.Index)
                                .ToList();

                            if (unitIndices.Count > 0)
                            {
                                int randomIndex = UnityEngine.Random.Range(0, unitIndices.Count);
                                DrawSpecificCard(player, unitIndices[randomIndex]);
                            }
                            else
                            {
                                // Fall back to utility logic if no units available
                                DrawUtilityCard(player);
                            }
                        }
                    }
                    else
                    {
                        // Draw a utility card
                        DrawUtilityCard(player);
                    }
                }
                else
                {
                    if (player.cards_deck.Count > 0 && player.cards_hand.Count < GameplayData.Get().cards_max)
                    {
                        Card card = player.cards_deck[0];
                        player.cards_deck.RemoveAt(0);
                        player.cards_hand.Add(card);
                        TriggerOtherCardsAbilityType(AbilityTrigger.OnOpponentDraw, card);
                        TriggerPlayerCardsAbilityType(player, AbilityTrigger.OnPlayerDraw);
                    }
                    else if (!player.is_ai)
                    {
                        // If unable to draw, take 3 damage
                        DamagePlayer(player, 3);
                    }
                }
            }

            if (player.cards_deck.Count == 0)
            {
                if (!player.is_ai)
                    DisplayAIMessageEvent(AIMessageEvent.PlayerLastCard);
                else
                    DisplayAIMessageEvent(AIMessageEvent.AILastCard);
            }

            onCardDrawn?.Invoke(nb);

            RefreshData();

            resolve_queue.ResolveAll(0.3f);
        }

        private void DrawSpecificCard(Player player, int index)
        {
            if (index >= 0 && index < player.cards_deck.Count)
            {
                Card card = player.cards_deck[index];
                player.cards_deck.RemoveAt(index);
                player.cards_hand.Add(card);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnOpponentDraw, card);
                TriggerPlayerCardsAbilityType(player, AbilityTrigger.OnPlayerDraw);
            }
        }

        private void DrawUtilityCard(Player player)
        {
            int miracleIndex = player.cards_deck.FindIndex(card => card.CardData.subtype == CardSubType.Miracle);
            if (miracleIndex != -1)
            {
                DrawSpecificCard(player, miracleIndex);
                return;
            }

            bool lowHp = player.hp < 8;
            bool lowMp = player.mana < 8;

            if (lowHp && !lowMp)
            {
                int spellIndex = player.cards_deck.FindIndex(card => card.CardData.subtype == CardSubType.Spell);
                if (spellIndex != -1)
                {
                    DrawSpecificCard(player, spellIndex);
                    return;
                }
            }
            else if (!lowHp && lowMp)
            {
                int skillIndex = player.cards_deck.FindIndex(card => card.CardData.subtype == CardSubType.Skill);
                if (skillIndex != -1)
                {
                    DrawSpecificCard(player, skillIndex);
                    return;
                }
            }
            else if (lowHp && lowMp)
            {
                int itemIndex = player.cards_deck.FindIndex(card => card.CardData.subtype == CardSubType.Item);
                if (itemIndex != -1)
                {
                    DrawSpecificCard(player, itemIndex);
                    return;
                }
            }

            // If no specific card was drawn, fall back to normal draw
            if (player.cards_deck.Count > 0 && player.cards_hand.Count < GameplayData.Get().cards_max)
            {
                Card card = player.cards_deck[0];
                player.cards_deck.RemoveAt(0);
                player.cards_hand.Add(card);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnOpponentDraw, card);
                TriggerPlayerCardsAbilityType(player, AbilityTrigger.OnPlayerDraw);
            }
        }

        //Put a card from deck into discard
        public virtual void DrawDiscardCard(Player player, int nb = 1)
        {
            for (int i = 0; i < nb; i++)
            {
                if (player.cards_deck.Count > 0)
                {
                    Card card = player.cards_deck[0];
                    player.cards_deck.RemoveAt(0);
                    player.cards_discard.Add(card);
                }
            }
        }

        //Summon copy of an existing card
        public virtual Card SummonCopy(Player player, Card copy, Slot slot)
        {
            CardData icard = copy.CardData;
            return SummonCard(player, icard, copy.VariantData, slot);
        }

        //Summon copy of an exiting card into hand
        public virtual Card SummonCopyHand(Player player, Card copy)
        {
            CardData icard = copy.CardData;
            return SummonCardHand(player, icard, copy.VariantData);
        }

        //Create a new card and send it to the board
        public virtual Card SummonCard(Player player, CardData card, VariantData variant, Slot slot)
        {
            if (!slot.IsValid())
                return null;

            if (game_data.GetSlotCard(slot) != null)
                return null;

            Card acard = SummonCardHand(player, card, variant);
            PlayCard(acard, slot, true);

            onCardSummoned?.Invoke(acard, slot);

            return acard;
        }

        //Create a new card and send it to your hand
        public virtual Card SummonCardHand(Player player, CardData card, VariantData variant)
        {
            Card acard = Card.Create(card, variant, player);
            player.cards_hand.Add(acard);
            game_data.last_summoned = acard.uid;
            return acard;
        }

        //Transform card into another one
        public virtual Card TransformCard(Card card, CardData transform_to)
        {
            card.SetCard(transform_to, card.VariantData);

            onCardTransformed?.Invoke(card);

            return card;
        }

        public virtual void EquipCard(Card card, Card equipment)
        {
            if (card != null && equipment != null && card.player_id == equipment.player_id)
            {
                if (!card.CardData.IsEquipment() && equipment.CardData.IsEquipment())
                {
                    UnequipAll(card); //Unequip previous cards, only 1 equip at a time

                    Player player = game_data.GetPlayer(card.player_id);
                    player.RemoveCardFromAllGroups(equipment);
                    player.cards_equip.Add(equipment);
                    card.equipped_uid = equipment.uid;
                    equipment.slot = card.slot;
                }
            }
        }

        public virtual void UnequipAll(Card card)
        {
            if (card != null && card.equipped_uid != null)
            {
                Player player = game_data.GetPlayer(card.player_id);
                Card equip = player.GetEquipCard(card.equipped_uid);
                if (equip != null)
                {
                    card.equipped_uid = null;
                    DiscardCard(equip);
                }
            }
        }

        //Change owner of a card
        public virtual void ChangeOwner(Card card, Player owner)
        {
            if (card.player_id != owner.player_id)
            {
                Player powner = game_data.GetPlayer(card.player_id);
                powner.RemoveCardFromAllGroups(card);
                powner.cards_all.Remove(card.uid);
                owner.cards_all[card.uid] = card;
                card.player_id = owner.player_id;
            }
        }

        //Damage a player without attacker
        public virtual void DamagePlayer(Player target, int value)
        {
            //Damage player
            target.hp -= value;
            target.hp = Mathf.Clamp(target.hp, 0, target.hp_max);
        }

        //Damage a player
        public virtual void DamagePlayer(Card attacker, Player target, int value)
        {
            //Damage player
            target.hp -= value;
            target.hp = Mathf.Clamp(target.hp, 0, target.hp_max);

            //Channel Life
            Player player = game_data.GetPlayer(attacker.player_id);
            if (attacker.HasStatus(StatusType.ChannelLife))
                HealPlayer(player, value);

            //Siphon
            if (attacker.HasStatus(StatusType.LifeSteal))
                HealCard(attacker, value);
        }

        //Heal a card
        public virtual void HealCard(Card target, int value)
        {
            if (target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return;

            target.damage -= value;
            target.damage = Mathf.Max(target.damage, 0);
        }

        public virtual void HealPlayer(Player target, int value)
        {
            if (target == null)
                return;

            target.hp += value;
            target.hp = Mathf.Clamp(target.hp, 0, target.hp_max);
        }

        //Generic damage that doesnt come from another card
        public virtual void DamageCard(Card target, int value)
        {
            if(target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return; //Invincible

            if (target.HasStatus(StatusType.SpellImmunity))
                return; //Spell immunity

            target.damage += value;

            if (target.GetHP() <= 0)
                DiscardCard(target);
        }
        public enum DamageType
        {
            Physical,
            Gun,
            Infernal,
            Frost,
            Aqua,
            Lightning,
            Terra,
            Holy,
            Shadow
        }

        //Damage a card with attacker/caster
        public virtual void DamageCard(Card attacker, Card target, int value, Element attackType = Element.None, Element attackType2 = Element.None)
        {
            int initialValue = value;
            target.lastHitDamageValue = 0;

            if (attacker == null || target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return; //Invincible

            if (target.HasStatus(StatusType.SpellImmunity) && attacker.CardData.type != CardType.Character)
                return; //Spell immunity

            if (attacker.CardData.IsBoardCard())
            {
                if (target.agility >= attacker.agility * 2)
                    return; //Attack Miss

                if (attacker.agility >= target.agility * 2 || target.IsIncapacitated())
                {
                    value += 3; //Critical Hit
                    GrantBonusAction(attacker);
                }
            }

           
            //Affinities
            value = AdjustDamageForAffinities(attacker, target, value, attackType, attackType2);
            //value = AdjustDamageForSlayerAbilities(attacker, target, value);

            int bonusDamage = value - initialValue;
            bonusDamage = Mathf.Max(bonusDamage, 0);

            //Defense
            value = Mathf.Max(value - target.GetDefense(), 0);

            //Shell
            bool doublelife = target.HasStatus(StatusType.Shell);
            if (doublelife && value > 0)
            {
                target.RemoveStatus(StatusType.Shell);
                return;
            }

            //Damage
            int damage_max = Mathf.Min(value, target.GetHP());
            damage_max = Mathf.Max(0, damage_max); // Should not heal from negative damage.
            value = Mathf.Max(0, value); // Should not heal from negative damage.
            int extra = value - target.GetHP();
            target.damage += damage_max;

            //Impact
            Player tplayer = game_data.GetPlayer(target.player_id);
            if (attacker.player_id == game_data.current_player && attacker.HasStatus(StatusType.Trample))
            {
                extra = Mathf.Max(extra, 0);
                tplayer.hp -= extra;
                tplayer.hp -= bonusDamage;
            }

            //Channel Life
            Player player = game_data.GetPlayer(attacker.player_id);
            if (attacker.HasStatus(StatusType.ChannelLife))
                HealPlayer(player, damage_max);

            //Siphon
            if (attacker.HasStatus(StatusType.LifeSteal))
                HealCard(attacker, damage_max);

            //Remove sleep on damage
            target.RemoveStatus(StatusType.Asleep);

            //Lethal
            if (value > 0 && attacker.HasStatus(StatusType.Deathtouch) && target.CardData.type == CardType.Character)
                KillCard(attacker, target);

            //Kill card if no hp
            if (target.GetHP() <= 0)
                KillCard(attacker, target);

            target.lastHitDamageValue = Mathf.Min(damage_max, target.GetHPMax());
        }

        public int AdjustDamageForAffinities(Card attacker, Card target, int baseDamage, Element attackElement, Element attackElement2 = Element.None)
        {


            int adjustedDamage = baseDamage;
            bool effectiveHit = attacker.HasStatus(StatusType.Fury);

            // Get the target's affinity to the attack element
            ElementalAffinity targetAffinity = attackElement switch
            {
                Element.Kinetic => target.Data.kineticAffinity,
                Element.Ballistic => target.Data.ballisticAffinity,
                Element.Arcane => target.Data.arcaneAffinity,
                Element.Infernal => target.Data.infernalAffinity,
                Element.Frost => target.Data.frostAffinity,
                Element.Terra => target.Data.terraAffinity,
                Element.Verdant => target.Data.verdantAffinity,
                Element.Volt => target.Data.voltAffinity,
                Element.Aqua => target.Data.aquaAffinity,
                Element.Gale => target.Data.galeAffinity,
                Element.Holy => target.Data.holyAffinity,
                Element.Umbral => target.Data.umbralAffinity,
                Element.Necrotic => target.Data.necroticAffinity,
                Element.Radiant => target.Data.radiantAffinity,
                Element.None => ElementalAffinity.Neutral,
                _ => ElementalAffinity.Neutral,
            };

            // Adjust damage based on affinity
            switch (targetAffinity)
            {
                case ElementalAffinity.Weak:
                    adjustedDamage += 3;
                    effectiveHit = true;
                    break;
                case ElementalAffinity.Resist:
                    if (!attacker.HasStatus(StatusType.Pierce))
                        adjustedDamage -= 2;
                    break;
                case ElementalAffinity.Void:
                    if (!attacker.HasStatus(StatusType.Pierce))
                    {
                        return 0; // Completely nullify damage if void affinity exists
                    }
                    break;
                case ElementalAffinity.Neutral:
                default:
                    break; // No adjustment for neutral affinity
            }

            if (effectiveHit)
            {
                bool isUnit = attacker.CardData.IsBoardCard();
                if (isUnit)
                {
                    GrantBonusAction(attacker);
                } else
                {
                    Player cardOwner = GameData.GetPlayer(attacker.player_id);
                    cardOwner.bonusAction = true;
                }
            }

            if(attackElement2 != Element.None)
            {
                return AdjustDamageForAffinities(attacker, target, adjustedDamage, attackElement2);
            }

            return AdjustDamageForAilments(attacker, target, adjustedDamage, attackElement, attackElement2);
        }

        public class ElementalAdjustment
        {
            public Element Element { get; set; }
            public int Stage { get; set; }
        }

        public class AilmentAdjustment
        {
            public StatusType Ailment { get; set; }
            public List<ElementalAdjustment> Resistances { get; set; }
            public List<ElementalAdjustment> Weaknesses { get; set; }
            public bool VoidAllExceptWeaknesses { get; set; }
        }

        public static readonly List<AilmentAdjustment> AilmentAdjustments = new List<AilmentAdjustment>
        {
            new AilmentAdjustment
            {
                Ailment = StatusType.Paralyzed,
                Weaknesses = new List<ElementalAdjustment>
                {
                    new ElementalAdjustment { Element = Element.Kinetic, Stage = 1 },
                    new ElementalAdjustment { Element = Element.Ballistic, Stage = 1 }
                }
            },
            new AilmentAdjustment
            {
                Ailment = StatusType.Asleep,
                Weaknesses = new List<ElementalAdjustment>
                {
                    new ElementalAdjustment { Element = Element.Umbral, Stage = 1 }
                }
            },
            new AilmentAdjustment
            {
                Ailment = StatusType.Poisoned,
                Weaknesses = new List<ElementalAdjustment>
                {
                    new ElementalAdjustment { Element = Element.Verdant, Stage = 1 }
                }
            },
            new AilmentAdjustment
            {
                Ailment = StatusType.Wet,
                Resistances = new List<ElementalAdjustment>
                {
                    new ElementalAdjustment { Element = Element.Infernal, Stage = 1 }
                },
                Weaknesses = new List<ElementalAdjustment>
                {
                    new ElementalAdjustment { Element = Element.Volt, Stage = 1 },
                    new ElementalAdjustment { Element = Element.Frost, Stage = 1 }
                }
            },
            new AilmentAdjustment
            {
                Ailment = StatusType.Submerged,
                Resistances = new List<ElementalAdjustment>
                {
                    new ElementalAdjustment { Element = Element.Infernal, Stage = 2 }
                },
                Weaknesses = new List<ElementalAdjustment>
                {
                    new ElementalAdjustment { Element = Element.Volt, Stage = 2 },
                    new ElementalAdjustment { Element = Element.Frost, Stage = 2 }
                }
            },
            new AilmentAdjustment
            {
                Ailment = StatusType.Encased,
                Weaknesses = new List<ElementalAdjustment>
                {
                    new ElementalAdjustment { Element = Element.Radiant, Stage = 2 },
                    new ElementalAdjustment { Element = Element.Frost, Stage = 2 }
                },
                VoidAllExceptWeaknesses = true
            },
            new AilmentAdjustment
            {
                Ailment = StatusType.Entombed,
                Weaknesses = new List<ElementalAdjustment>
                {
                    new ElementalAdjustment { Element = Element.Umbral, Stage = 2 },
                    new ElementalAdjustment { Element = Element.Terra, Stage = 2 }
                },
                VoidAllExceptWeaknesses = true
            },
            new AilmentAdjustment
            {
                Ailment = StatusType.Frozen,
                Resistances = new List<ElementalAdjustment>
                {
                    new ElementalAdjustment { Element = Element.Infernal, Stage = 1 }
                },
                Weaknesses = new List<ElementalAdjustment>
                {
                    new ElementalAdjustment { Element = Element.Frost, Stage = 2 },
                    new ElementalAdjustment { Element = Element.Aqua, Stage = 2 }
                }
            }
        };

        public int AdjustDamageForAilments(Card attacker, Card target, int baseDamage, Element attackElement, Element attackElement2 = Element.None)
        {
            int adjustedDamage = baseDamage;

            foreach (var ailmentAdjustment in AilmentAdjustments)
            {
                if (target.HasStatus(ailmentAdjustment.Ailment))
                {
                    adjustedDamage = ApplyElementalAdjustment(adjustedDamage, attackElement, ailmentAdjustment);
                    if (adjustedDamage == 0) break; // If damage is reduced to 0, no need to check further ailments
                }
            }

            if (attackElement2 != Element.None && adjustedDamage > 0)
            {
                return AdjustDamageForAilments(attacker, target, adjustedDamage, attackElement2);
            }

            return adjustedDamage;
        }

        private int ApplyElementalAdjustment(int damage, Element attackElement, AilmentAdjustment ailmentAdjustment)
        {
            if (ailmentAdjustment.VoidAllExceptWeaknesses)
            {
                var weakness = ailmentAdjustment.Weaknesses?.FirstOrDefault(w => w.Element == attackElement);
                if (weakness == null)
                {
                    return 0; // Void all damage except for weaknesses
                }
                else
                {
                    return weakness.Stage switch
                    {
                        1 => damage + 1,
                        2 => damage + 2,
                        _ => damage
                    };
                }
            } else
            {
                var resistance = ailmentAdjustment.Resistances?.FirstOrDefault(r => r.Element == attackElement);
                var weakness = ailmentAdjustment.Weaknesses?.FirstOrDefault(w => w.Element == attackElement);

                if (resistance != null)
                {
                    return resistance.Stage switch
                    {
                        1 => Math.Max(damage - 1, 0),
                        2 => 0,
                        _ => damage
                    };
                }
                else if (weakness != null)
                {
                    return weakness.Stage switch
                    {
                        1 => damage + 1,
                        2 => damage + 2,
                        _ => damage
                    };
                }

                return damage;
            }
        }

        public void AdjustStatusesAfterDamage(Card target, int damageDealt, Element attackElement, Element attackElement2 = Element.None)
        {
            AdjustStatusForElement(target, damageDealt, attackElement);

            if (attackElement2 != Element.None)
            {
                AdjustStatusForElement(target, damageDealt, attackElement2);
            }
        }

        private void AdjustStatusForElement(Card target, int damageDealt, Element attackElement)
        {
            List<StatusType> statusesToRemove = new List<StatusType>();
            List<StatusType> statusesToAdd = new List<StatusType>();

            foreach (CardStatus cardStatus in target.GetAllStatus())
            {
                StatusType status = cardStatus.type;
                switch (status)
                {
                    case StatusType.Asleep:
                        if (attackElement == Element.Kinetic)
                        {
                            statusesToRemove.Add(StatusType.Asleep);
                        }
                        break;

                    case StatusType.Bleeding:
                        if (target.GetHP() < target.GetHPMax() * 0.5f)
                        {
                            statusesToAdd.Add(StatusType.Unconscious);
                        }
                        break;

                    case StatusType.Encased:
                        if (attackElement == Element.Radiant || attackElement == Element.Frost)
                        {
                            statusesToRemove.Add(StatusType.Encased);
                        }
                        break;

                    case StatusType.Entombed:
                        if (attackElement == Element.Umbral || attackElement == Element.Terra)
                        {
                            statusesToRemove.Add(StatusType.Entombed);
                        }
                        break;

                    case StatusType.Frozen:
                        if (attackElement == Element.Infernal)
                        {
                            statusesToRemove.Add(StatusType.Frozen);
                            statusesToAdd.Add(StatusType.Wet);
                        }
                        break;

                    case StatusType.Submerged:
                        if (attackElement == Element.Terra)
                        {
                            SpreadStatusToAllies(target, StatusType.Wet);
                        }
                        break;

                        // Other statuses like Paralysed, Poisoned, Wet don't have immediate effects from damage
                        // So we don't need to handle them here
                }
            }

            // Apply the status changes after processing all statuses
            foreach (StatusType status in statusesToRemove)
            {
                target.RemoveStatus(status);
            }

            foreach (StatusType status in statusesToAdd)
            {
                target.AddStatus(status, 0, 0);
            }
        }

        private void SpreadStatusToAllies(Card sourceUnit, StatusType status)
        {
            if (sourceUnit == null || game_data == null)
                return;

            Player player = game_data.GetPlayer(sourceUnit.player_id);
            if (player == null)
                return;

            foreach (Card allyCard in player.cards_board)
            {
                if (allyCard != sourceUnit)
                {
                    // Apply the status to the ally
                    allyCard.AddStatus(status, 0, 0); 
                }
            }

            RefreshData();
        }


        // Helper method to reset Submerged counter when the status is applied
        public void ResetSubmergedCounter(Card unit)
        {
            unit.StatusCounters[StatusType.Submerged] = 0;
        }

        public int AdjustDamageForSlayerAbilities(Card attacker, Card target, int baseDamage)
        {
            int adjustedDamage = baseDamage;

            // Trait Slayers

            if (attacker.HasStatus(StatusType.GodSlayer) && target.HasTrait("god"))
                adjustedDamage += 3;

            if (attacker.HasStatus(StatusType.DemonSlayer) && target.HasTrait("demon"))
                adjustedDamage += 3;

            if (attacker.HasStatus(StatusType.AngelSlayer) && target.HasTrait("angel"))
                adjustedDamage += 3;

            if (attacker.HasStatus(StatusType.KingSlayer) && target.HasTrait("lord"))
                adjustedDamage += 3;

            // Status Slayers

            if (attacker.HasStatus(StatusType.VanguardSlayer) && target.HasStatus(StatusType.Protection))
                adjustedDamage += 3;

            if (attacker.HasStatus(StatusType.DreadSlayer) && target.HasStatus(StatusType.Intimidate))
                adjustedDamage += 3;

            if (attacker.HasStatus(StatusType.ArmorSlayer) && target.GetStatusValue(StatusType.Armor) > 0)
                adjustedDamage += 3;

            return adjustedDamage;
        }

        //A card that kills another card
        public virtual void KillCard(Card attacker, Card target)
        {
            if (attacker == null || target == null)
                return;

            if (!game_data.IsOnBoard(target) && !game_data.IsEquipped(target))
                return; //Already killed

            if (target.HasStatus(StatusType.Invincibility))
                return; //Cant be killed

            Player pattacker = game_data.GetPlayer(attacker.player_id);
            if (attacker.player_id != target.player_id)
                pattacker.kill_count++;

            if (target.HasTrait("lord"))
            {
                if (!pattacker.is_ai)
                    DisplayAIMessageEvent(AIMessageEvent.PlayerDestroyKeyUnit);
                else
                    DisplayAIMessageEvent(AIMessageEvent.AIDestroyKeyUnit);
            }

            if (target.HasTrait("fusion"))
            {
                if (!pattacker.is_ai)
                    DisplayAIMessageEvent(AIMessageEvent.PlayerCounterAIStrategy);
                else
                    DisplayAIMessageEvent(AIMessageEvent.AICounterPlayerStrategy);
            }

            DiscardCard(target);

            TriggerCardAbilityType(AbilityTrigger.OnKill, attacker, target);
        }

        //Send card into discard
        public virtual void DiscardCard(Card card)
        {
            if (card == null)
                return;

            if (game_data.IsInDiscard(card))
                return; //Already discarded

            CardData icard = card.CardData;
            Player player = game_data.GetPlayer(card.player_id);
            bool was_on_board = game_data.IsOnBoard(card) || game_data.IsEquipped(card);

            //Unequip card
            UnequipAll(card);

            //Remove card from board and add to discard
            player.RemoveCardFromAllGroups(card);
            player.cards_discard.Add(card);
            game_data.last_destroyed = card.uid;

            //Remove from bearer
            Card bearer = player.GetBearerCard(card);
            if (bearer != null)
                bearer.equipped_uid = null;

            if (was_on_board)
            {
                //Trigger on death abilities
                TriggerCardAbilityType(AbilityTrigger.OnDeath, card);
                TriggerCardAbilityType(AbilityTrigger.OnRemovedFromPlay, card);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnDeathOther, card);
                TriggerSecrets(AbilityTrigger.OnDeathOther, card);
            }

            cards_to_clear.Add(card); //Will be Clear() in the next UpdateOngoing, so that simultaneous damage effects work
            onCardDiscarded?.Invoke(card);
        }

        public int RollRandomValue(int dice)
        {
            return RollRandomValue(1, dice + 1);
        }

        public virtual int RollRandomValue(int min, int max)
        {
            game_data.rolled_value = random.Next(min, max);
            onRollValue?.Invoke(game_data.rolled_value);
            resolve_queue.SetDelay(1f);
            return game_data.rolled_value;
        }

        //--- Abilities --

        public virtual void TriggerCardAbilityType(AbilityTrigger type, Card caster, Card triggerer = null)
        {
            foreach (AbilityData iability in caster.GetAbilities())
            {
                if (iability && iability.trigger == type)
                {
                    TriggerCardAbility(iability, caster, triggerer);
                }
            }

            Card equipped = game_data.GetEquipCard(caster.equipped_uid);
            if(equipped != null)
                TriggerCardAbilityType(type, equipped, triggerer);
        }

        public virtual void TriggerCardAbilityType(AbilityTrigger type, Card caster, Player triggerer)
        {
            foreach (AbilityData iability in caster.GetAbilities())
            {
                if (iability && iability.trigger == type)
                {
                    TriggerCardAbility(iability, caster, triggerer);
                }
            }

            Card equipped = game_data.GetEquipCard(caster.equipped_uid);
            if (equipped != null)
                TriggerCardAbilityType(type, equipped, triggerer);
        }
        
        public virtual void TriggerOtherCardsAbilityType(AbilityTrigger type, Card triggerer)
        {
            foreach (Player oplayer in game_data.players)
            {
                if(oplayer.hero != null)
                    TriggerCardAbilityType(type, oplayer.hero, triggerer);

                foreach (Card card in oplayer.cards_board)
                    TriggerCardAbilityType(type, card, triggerer);
            }
        }

        public virtual void TriggerPlayerCardsAbilityType(Player player, AbilityTrigger type)
        {
            if (player.hero != null)
                TriggerCardAbilityType(type, player.hero, player.hero);

            foreach (Card card in player.cards_board)
                TriggerCardAbilityType(type, card, card);
        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster, Card triggerer = null)
        {
            Card trigger_card = triggerer != null ? triggerer : caster; //Triggerer is the caster if not set
            if (!caster.HasStatus(StatusType.Silenced) && iability.AreTriggerConditionsMet(game_data, caster, trigger_card))
            {
                resolve_queue.AddAbility(iability, caster, trigger_card, ResolveCardAbility);
            }
        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster, Player triggerer)
        {
            if (!caster.HasStatus(StatusType.Silenced) && iability.AreTriggerConditionsMet(game_data, caster, triggerer))
            {
                resolve_queue.AddAbility(iability, caster, caster, ResolveCardAbility);
            }
        }

        //Resolve a card ability, may stop to ask for target
        protected virtual void ResolveCardAbility(AbilityData iability, Card caster, Card triggerer)
        {
            if (!caster.CanDoAbilities())
                return; //Silenced card cant cast

            //Debug.Log("Trigger Ability " + iability.id + " : " + caster.card_id);

            onAbilityStart?.Invoke(iability, caster);
            game_data.ability_triggerer = triggerer.uid;

            bool is_selector = ResolveCardAbilitySelector(iability, caster);
            if (is_selector)
                return; //Wait for player to select

            ResolveCardAbilityPlayTarget(iability, caster);
            ResolveCardAbilityPlayers(iability, caster);
            ResolveCardAbilityCards(iability, caster);
            ResolveCardAbilitySlots(iability, caster);
            ResolveCardAbilityCardData(iability, caster);
            ResolveCardAbilityNoTarget(iability, caster);
            AfterAbilityResolved(iability, caster);
        }

        protected virtual bool ResolveCardAbilitySelector(AbilityData iability, Card caster)
        {
            if (iability.target == AbilityTarget.SelectTarget)
            {
                //Wait for target
                GoToSelectTarget(iability, caster);
                return true;
            }
            else if (iability.target == AbilityTarget.CardSelector)
            {
                GoToSelectorCard(iability, caster);
                return true;
            }
            else if (iability.target == AbilityTarget.ChoiceSelector)
            {
                GoToSelectorChoice(iability, caster);
                return true;
            }
            return false;
        }

        protected virtual void ResolveCardAbilityPlayTarget(AbilityData iability, Card caster)
        {
            if (iability.target == AbilityTarget.PlayTarget)
            {
                Slot slot = caster.slot;
                Card slot_card = game_data.GetSlotCard(slot);
                if (slot.IsPlayerSlot())
                {
                    Player tplayer = game_data.GetPlayer(slot.p);
                    if (iability.CanTarget(game_data, caster, tplayer))
                        ResolveEffectTarget(iability, caster, tplayer);
                }
                else if (slot_card != null)
                {
                    if (iability.CanTarget(game_data, caster, slot_card))
                        ResolveEffectTarget(iability, caster, slot_card);
                }
                else
                {
                    if (iability.CanTarget(game_data, caster, slot))
                        ResolveEffectTarget(iability, caster, slot);
                }
            }
        }

        protected virtual void ResolveCardAbilityPlayers(AbilityData iability, Card caster)
        {
            //Get Player Targets based on conditions
            List<Player> targets = iability.GetPlayerTargets(game_data, caster, player_array);

            //Resolve effects
            foreach (Player target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityCards(AbilityData iability, Card caster)
        {
            //Get Cards Targets based on conditions
            List<Card> targets = iability.GetCardTargets(game_data, caster, card_array);

            //Resolve effects
            foreach (Card target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilitySlots(AbilityData iability, Card caster)
        {
            //Get Slot Targets based on conditions
            List<Slot> targets = iability.GetSlotTargets(game_data, caster, slot_array);

            //Resolve effects
            foreach (Slot target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityCardData(AbilityData iability, Card caster)
        {
            //Get Cards Targets based on conditions
            List<CardData> targets = iability.GetCardDataTargets(game_data, caster, card_data_array);

            //Resolve effects
            foreach (CardData target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityNoTarget(AbilityData iability, Card caster)
        {
            if (iability.target == AbilityTarget.None)
                iability.DoEffects(this, caster);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Player target)
        {
            iability.DoEffects(this, caster, target);

            onAbilityTargetPlayer?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Card target)
        {
            iability.DoEffects(this, caster, target);

            onAbilityTargetCard?.Invoke(iability, caster, target);
            game_data.last_target = target.uid;
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Slot target)
        {
            iability.DoEffects(this, caster, target);

            onAbilityTargetSlot?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, CardData target)
        {
            iability.DoEffects(this, caster, target);
        }

        protected virtual void AfterAbilityResolved(AbilityData iability, Card caster)
        {
            Player player = game_data.GetPlayer(caster.player_id);

            //Add to played
            game_data.ability_played.Add(iability.id);

            if (iability.charge_player_action)
            {
                player.useAction(!iability.half_action);
            } else
            {
                //Pay cost
                if (iability.trigger == AbilityTrigger.Activate || iability.trigger == AbilityTrigger.None)
                {
                    //player.mana -= iability.mana_cost;
                    if (iability.ability_type == AbilityType.Spell)
                    {
                        caster.mp -= iability.mana_cost;
                    }
                    else
                    {
                        caster.damage += iability.mana_cost;
                    }

                    ReduceActionValue(caster);
                }
            }

            //Recalculate and clear
            UpdateOngoing();
            CheckForWinner();

            //Chain ability
            if (iability.target != AbilityTarget.ChoiceSelector && game_data.state != GameState.GameEnded)
            {
                foreach (AbilityData chain_ability in iability.chain_abilities)
                {
                    if (chain_ability != null)
                    {
                        TriggerCardAbility(chain_ability, caster);
                    }
                }
            }

            onAbilityEnd?.Invoke(iability, caster);
            resolve_queue.ResolveAll(0.5f);
            RefreshData();
        }

        //This function is called often to update status/stats affected by ongoing abilities
        //It basically first reset the bonus to 0 (CleanOngoing) and then recalculate it to make sure it it still present
        //Only cards in hand and on board are updated in this way
        public virtual void UpdateOngoing()
        {
            Profiler.BeginSample("Update Ongoing");
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                player.ClearOngoing();

                for (int c = 0; c < player.cards_board.Count; c++)
                    player.cards_board[c].ClearOngoing();

                for (int c = 0; c < player.cards_equip.Count; c++)
                    player.cards_equip[c].ClearOngoing();

                for (int c = 0; c < player.cards_hand.Count; c++)
                    player.cards_hand[c].ClearOngoing();
            }

            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                UpdateOngoingAbilities(player, player.hero);  //Remove this line if hero is on the board

                for (int c = 0; c < player.cards_board.Count; c++)
                {
                    Card card = player.cards_board[c];
                    UpdateOngoingAbilities(player, card);
                }

                for (int c = 0; c < player.cards_equip.Count; c++)
                {
                    Card card = player.cards_equip[c];
                    UpdateOngoingAbilities(player, card);
                }
            }

            //Stats bonus
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                for(int c=0; c<player.cards_board.Count; c++)
                {
                    Card card = player.cards_board[c];

                    //Taunt effect
                    if (card.HasStatus(StatusType.Protection) && !card.HasStatus(StatusType.Stealth))
                    {
                        player.AddOngoingStatus(StatusType.Protected, 0);

                        for (int tc = 0; tc < player.cards_board.Count; tc++)
                        {
                            Card tcard = player.cards_board[tc];
                            if (!tcard.HasStatus(StatusType.Protection) && !tcard.HasStatus(StatusType.Protected))
                            {
                                tcard.AddOngoingStatus(StatusType.Protected, 0);
                            }
                        }
                    }

                    //Status bonus
                    foreach (CardStatus status in card.status)
                        AddOngoingStatusBonus(card, status);
                    foreach (CardStatus status in card.ongoing_status)
                        AddOngoingStatusBonus(card, status);
                }

                for (int c = 0; c < player.cards_hand.Count; c++)
                {
                    Card card = player.cards_hand[c];
                    //Status bonus
                    foreach (CardStatus status in card.status)
                        AddOngoingStatusBonus(card, status);
                    foreach (CardStatus status in card.ongoing_status)
                        AddOngoingStatusBonus(card, status);
                }
            }

            //Kill stuff with 0 hp
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                for (int i = player.cards_board.Count - 1; i >= 0; i--)
                {
                    Card card = player.cards_board[i];
                    if (card.GetHP() <= 0)
                        DiscardCard(card);
                }
                for (int i = player.cards_equip.Count - 1; i >= 0; i--)
                {
                    Card card = player.cards_equip[i];
                    if (card.GetHP() <= 0)
                        DiscardCard(card);
                    Card bearer = player.GetBearerCard(card);
                    if(bearer == null)
                        DiscardCard(card);
                }
            }

            //Clear cards
            for (int c = 0; c < cards_to_clear.Count; c++)
                cards_to_clear[c].Clear();
            cards_to_clear.Clear();

            Profiler.EndSample();
        }

        protected virtual void UpdateOngoingAbilities(Player player, Card card)
        {
            if (card == null || !card.CanDoAbilities())
                return;

            List<AbilityData> cabilities = card.GetAbilities();
            for (int a = 0; a < cabilities.Count; a++)
            {
                AbilityData ability = cabilities[a];
                if (ability != null && ability.trigger == AbilityTrigger.Ongoing && ability.AreTriggerConditionsMet(game_data, card))
                {
                    if (ability.target == AbilityTarget.Self)
                    {
                        if (ability.AreTargetConditionsMet(game_data, card, card))
                        {
                            ability.DoOngoingEffects(this, card, card);
                        }
                    }

                    if (ability.target == AbilityTarget.PlayerSelf)
                    {
                        if (ability.AreTargetConditionsMet(game_data, card, player))
                        {
                            ability.DoOngoingEffects(this, card, player);
                        }
                    }

                    if (ability.target == AbilityTarget.AllPlayers || ability.target == AbilityTarget.PlayerOpponent)
                    {
                        for (int tp = 0; tp < game_data.players.Length; tp++)
                        {
                            if (ability.target == AbilityTarget.AllPlayers || tp != player.player_id)
                            {
                                Player oplayer = game_data.players[tp];
                                if (ability.AreTargetConditionsMet(game_data, card, oplayer))
                                {
                                    ability.DoOngoingEffects(this, card, oplayer);
                                }
                            }
                        }
                    }

                    if (ability.target == AbilityTarget.EquippedCard)
                    {
                        if (card.CardData.IsEquipment())
                        {
                            //Get bearer of the equipment
                            Card target = player.GetBearerCard(card);
                            if (target != null && ability.AreTargetConditionsMet(game_data, card, target))
                            {
                                ability.DoOngoingEffects(this, card, target);
                            }
                        }
                        else if (card.equipped_uid != null)
                        {
                            //Get equipped card
                            Card target = game_data.GetCard(card.equipped_uid);
                            if (target != null && ability.AreTargetConditionsMet(game_data, card, target))
                            {
                                ability.DoOngoingEffects(this, card, target);
                            }
                        }
                    }

                    if (ability.target == AbilityTarget.AllCardsAllPiles || ability.target == AbilityTarget.AllCardsHand || ability.target == AbilityTarget.AllCardsBoard)
                    {
                        for (int tp = 0; tp < game_data.players.Length; tp++)
                        {
                            //Looping on all cards is very slow, since there are no ongoing effects that works out of board/hand we loop on those only
                            Player tplayer = game_data.players[tp];

                            //Hand Cards
                            if (ability.target == AbilityTarget.AllCardsAllPiles || ability.target == AbilityTarget.AllCardsHand)
                            {
                                for (int tc = 0; tc < tplayer.cards_hand.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_hand[tc];
                                    if (ability.AreTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }

                            //Board Cards
                            if (ability.target == AbilityTarget.AllCardsAllPiles || ability.target == AbilityTarget.AllCardsBoard)
                            {
                                for (int tc = 0; tc < tplayer.cards_board.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_board[tc];
                                    if (ability.AreTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }

                            //Equip Cards
                            if (ability.target == AbilityTarget.AllCardsAllPiles)
                            {
                                for (int tc = 0; tc < tplayer.cards_equip.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_equip[tc];
                                    if (ability.AreTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        protected virtual void AddOngoingStatusBonus(Card card, CardStatus status)
        {
            if (status.type == StatusType.AddAttack)
                card.attack_ongoing += status.value;
            if (status.type == StatusType.AddDefense)
                card.defense_ongoing += status.value;
            if (status.type == StatusType.AddHP)
                card.hp_ongoing += status.value;
            if (status.type == StatusType.AddManaCost)
                card.mana_ongoing += status.value;
            if (status.type == StatusType.AddAgility)
                card.agility_ongoing += status.value;
        }

        //---- Secrets ------------

        public virtual bool TriggerPlayerSecrets(Player player, AbilityTrigger secret_trigger)
        {
            for (int i = player.cards_secret.Count - 1; i >= 0; i--)
            {
                Card card = player.cards_secret[i];
                CardData icard = card.CardData;
                if (icard.type == CardType.Secret)
                {
                    if (card.AreAbilityConditionsMet(secret_trigger, game_data, card, card))
                    {
                        resolve_queue.AddSecret(secret_trigger, card, card, ResolveSecret);
                        resolve_queue.SetDelay(0.5f);

                        if (onSecretTrigger != null)
                            onSecretTrigger.Invoke(card, card);

                        return true; //Trigger only 1 secret per trigger
                    }
                }
            }
            return false;
        }

        public virtual bool TriggerSecrets(AbilityTrigger secret_trigger, Card trigger_card)
        {
            if (trigger_card != null && trigger_card.HasStatus(StatusType.SpellImmunity))
                return false; //Spell Immunity, triggerer is the one that trigger the trap, target is the one attacked, so usually the player who played the trap, so we dont check the target

            for(int p=0; p < game_data.players.Length; p++ )
            {
                if (p != game_data.current_player)
                {
                    Player other_player = game_data.players[p];
                    Player current_player = game_data.players[game_data.current_player];
                    if (current_player.cards_board.Any(card => card.HasStatus(StatusType.DisableEnemyWards)))
                        return false;
                    for (int i = other_player.cards_secret.Count - 1; i >= 0; i--)
                    {
                        Card card = other_player.cards_secret[i];
                        CardData icard = card.CardData;
                        if (icard.type == CardType.Secret && !card.HasStatus(StatusType.Silenced))
                        {
                            Card trigger = trigger_card != null ? trigger_card : card;
                            if (card.AreAbilityConditionsMet(secret_trigger, game_data, card, trigger))
                            {
                                resolve_queue.AddSecret(secret_trigger, card, trigger, ResolveSecret);
                                resolve_queue.SetDelay(0.5f);

                                if (onSecretTrigger != null)
                                    onSecretTrigger.Invoke(card, trigger);

                                return true; //Trigger only 1 secret per trigger
                            }
                        }
                    }
                }
            }
            return false;
        }

        protected virtual void ResolveSecret(AbilityTrigger secret_trigger, Card secret_card, Card trigger)
        {
            CardData icard = secret_card.CardData;
            Player player = game_data.GetPlayer(secret_card.player_id);
            if (icard.type == CardType.Secret)
            {
                Player tplayer = game_data.GetPlayer(trigger.player_id);
                if(!is_ai_predict)
                    tplayer.AddHistory(GameAction.SecretTriggered, secret_card, trigger);

                TriggerCardAbilityType(secret_trigger, secret_card, trigger);
                DiscardCard(secret_card);

                if (onSecretResolve != null)
                    onSecretResolve.Invoke(secret_card, trigger);
            }
        }

        //---- Resolve Selector -----

        public virtual void SelectCard(Card target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || target == null || ability == null)
                return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if (!ability.CanTarget(game_data, caster, target))
                    return; //Can't target that target

                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                game_data.selector = SelectorType.None;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster);
                resolve_queue.ResolveAll();
            }

            if (game_data.selector == SelectorType.SelectorCard)
            {
                if (!ability.IsCardSelectionValid(game_data, caster, target, card_array))
                    return; //Supports conditions and filters

                game_data.selector = SelectorType.None;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectPlayer(Player target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || target == null || ability == null)
                return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if (!ability.CanTarget(game_data, caster, target))
                    return; //Can't target that target

                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                game_data.selector = SelectorType.None;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectSlot(Slot target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || ability == null || !target.IsValid())
                return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if(!ability.CanTarget(game_data, caster, target))
                    return; //Conditions not met

                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                game_data.selector = SelectorType.None;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectChoice(int choice)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || ability == null || choice < 0)
                return;

            if (game_data.selector == SelectorType.SelectorChoice && ability.target == AbilityTarget.ChoiceSelector)
            {
                if (choice >= 0 && choice < ability.chain_abilities.Length)
                {
                    AbilityData achoice = ability.chain_abilities[choice];
                    if (achoice != null && game_data.CanSelectAbility(caster, achoice))
                    {
                        game_data.selector = SelectorType.None;
                        AfterAbilityResolved(ability, caster);
                        ResolveCardAbility(achoice, caster, caster);
                        resolve_queue.ResolveAll();
                    }
                }
            }
        }

        public virtual void CancelSelection()
        {
            if (game_data.selector != SelectorType.None)
            {
                //End selection
                game_data.selector = SelectorType.None;
                RefreshData();
            }
        }

        //-----Trigger Selector-----

        protected virtual void GoToSelectTarget(AbilityData iability, Card caster)
        {
            game_data.selector = SelectorType.SelectTarget;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = iability.id;
            game_data.selector_caster_uid = caster.uid;
            RefreshData();
        }

        protected virtual void GoToSelectorCard(AbilityData iability, Card caster)
        {
            game_data.selector = SelectorType.SelectorCard;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = iability.id;
            game_data.selector_caster_uid = caster.uid;
            RefreshData();
        }

        protected virtual void GoToSelectorChoice(AbilityData iability, Card caster)
        {
            game_data.selector = SelectorType.SelectorChoice;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = iability.id;
            game_data.selector_caster_uid = caster.uid;
            RefreshData();
        }

        //-------------

        public virtual void RefreshData()
        {
            onRefresh?.Invoke();
        }

        public virtual void ClearResolve()
        {
            resolve_queue.Clear();
        }

        public virtual bool IsResolving()
        {
            return resolve_queue.IsResolving();
        }

        public virtual bool IsGameStarted()
        {
            return game_data.HasStarted();
        }

        public virtual bool IsGameEnded()
        {
            return game_data.HasEnded();
        }

        public virtual Game GetGameData()
        {
            return game_data;
        }

        public System.Random GetRandom()
        {
            return random;
        }

        public Game GameData { get { return game_data; } }
        public ResolveQueue ResolveQueue { get { return resolve_queue; } }

        // AI Story/Dialogue Helpers

        private void DisplayAIMessage(string message)
        {
            if (is_ai_predict == false && has_ai_player)
            {
                GameClient.Get().onChatMsg(1, message);
            }
        }

        private void DisplayAIMessageEvent(AIMessageEvent event_type)
        {
            if (is_ai_predict == false && has_ai_player)
            {
                DateTime current_time = DateTime.Now;
                TimeSpan time_since_last_message = current_time - last_message_display_time;
                if (time_since_last_message.TotalSeconds >= MIN_SECONDS_BETWEEN_MESSAGES)
                {
                    string message = DialogueManager.Get().GetMessage(event_type, ai_character);
                    if (message != null && !string.IsNullOrEmpty(message))
                    {
                        DisplayAIMessage(message);
                        last_message_display_time = current_time;
                    }
                }
                // If less than 30 seconds have passed, the message is silently skipped
            }
        }

        // Function to get a random item from a list
        public string GetRandomItem(List<string> list)
        {
            if (list == null || list.Count == 0)
                return null;
            return list[random.Next(list.Count)];
        }



        private List<string> GetAISleeves(AICharacter character)
        {
            Dictionary<AICharacter, List<string>> sleeves = new Dictionary<AICharacter, List<string>>
            {
                { AICharacter.SebastionVonAdler, new List<string> { "as_poseidon", "as_mermaid" } },
                { AICharacter.ClaraFischer, new List<string> { "as_study", "as_displace" } }
            };

            List<string> defaultSleeves = new List<string>();
            defaultSleeves.Add("as_displace");
            defaultSleeves.Add("as_erasure");
            defaultSleeves.Add("fs_aurelia");
            defaultSleeves.Add("as_study");
            defaultSleeves.Add("fs_albina_marina_1");

            return sleeves.TryGetValue(character, out var characterSleeves) ? characterSleeves : defaultSleeves;
        }

        private List<string> GetAIMats(AICharacter character)
        {
            Dictionary<AICharacter, List<string>> mats = new Dictionary<AICharacter, List<string>>
            {
                { AICharacter.SebastionVonAdler, new List<string> { "ms_mermaid_1", "ms_mermaid_2" } },
            };

            List<string> defaultMats = new List<string>();
            defaultMats.Add("rs_ghabat");
            defaultMats.Add("rs_vastus");
            defaultMats.Add("rs_deshret");
            defaultMats.Add("rs_hrimfjall");
            defaultMats.Add("rs_inochi");

            return mats.TryGetValue(character, out var characterMats) ? characterMats : defaultMats;
        }

        public static readonly Dictionary<StatusType, (int flatDamage, float percentageMaxHP)> dotValues = new Dictionary<StatusType, (int, float)>
        {
            { StatusType.Bleeding, (1, 0.05f) },   // 1 flat damage + 5% of max HP
            { StatusType.Ablaze, (2, 0.08f) },     // 2 flat damage + 8% of max HP
            { StatusType.Poisoned, (1, 0.06f) },   // 1 flat damage + 6% of max HP
            { StatusType.InfectedI, (1, 0.04f) },  // 1 flat damage + 4% of max HP
            { StatusType.InfectedII, (2, 0.06f) }, // 2 flat damage + 6% of max HP
            { StatusType.InfectedIII, (3, 0.08f) },// 3 flat damage + 8% of max HP
            { StatusType.Nightmare, (0, 0.07f) },  // 7% of max HP
            { StatusType.Frozen, (0, 0.03f) },     // 3% of max HP
            { StatusType.Drowning, (0, 0.5f) },    // 50% of max HP
            { StatusType.Overclocked, (1, 0) }     // 1 flat damage
        };

        public static int CalculateDOTDamage(Card card, StatusType statusType)
        {
            if (!dotValues.ContainsKey(statusType))
                return 0;

            var (flatDamage, percentageMaxHP) = dotValues[statusType];
            int percentageDamage = (int)Math.Ceiling(card.GetHPMax() * percentageMaxHP);
            return flatDamage + percentageDamage;
        }

        private string GetCharacterUsername(AICharacter character)
        {
            switch (character)
            {
                case AICharacter.ClaraFischer:
                    return "Clara Fischer";
                case AICharacter.JamalHarris:
                    return "Jamal Harris";
                case AICharacter.LydiaKorovin:
                    return "Lydia Korovin";
                case AICharacter.MateoRamirez:
                    return "Mateo Ramirez";
                case AICharacter.MikaelRodriguez:
                    return "Mikael Rodriguez";
                case AICharacter.RachelMorgan:
                    return "Rachel Morgan";
                case AICharacter.SarahRegnitz:
                    return "Sarah Regnitz";
                case AICharacter.SebastionVonAdler:
                    return "Sebastion Von Adler";
                case AICharacter.SophiaWeber:
                    return "Sophia Weber";
                case AICharacter.TylerJohnson:
                    return "Tyler Johnson";
                default:
                    return "";
            }
        }

        private void SetupAICharacterLogic(Player player)
        {
            if (player.is_ai)
            {
                switch (player.deck)
                {
                    case "tylers_sabers":
                        ai_character = AICharacter.TylerJohnson;
                        break;
                    case "rachels_gelatinous":
                        ai_character = AICharacter.RachelMorgan;
                        break;
                    case "mateos_balthazar":
                        ai_character = AICharacter.MateoRamirez;
                        break;
                    default:
                        ai_character = AICharacter.RachelMorgan;
                        break;
                }
                SetupAICharacterLogic(player, ai_character);
            }
        }
        private void SetupAICharacterLogic(Player player, AICharacter in_ai_character)
        {
            if (player.is_ai)
            {
                has_ai_player = true;
                ai_character = in_ai_character;

                DialogueManager.Get().Start();
                DialogueManager.Get().ResetAllUsedMessages();

                string opponentName = GetCharacterUsername(ai_character);
                player.username = opponentName;
                player.avatar = opponentName;
                player.cardback = GetRandomItem(GetAISleeves(ai_character));
                player.mat = GetRandomItem(GetAIMats(ai_character));
            }
        }
    }
}