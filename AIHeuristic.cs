using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TcgEngine.Gameplay;
using UnityEngine;

namespace TcgEngine.AI
{
    /// <summary>
    /// Values and calculations for various values of the AI decision-making, adjusting these can improve your AI
    /// Heuristic: Represent the score of a board state, high score favor AI, low score favor the opponent
    /// Action Score: Represent the score of an individual action, to proritize actions if too many in a single node
    /// Action Sort Order: Value to determine the order actions should be executed in a single turn to avoid searching same things in different order, executed in ascending order
    /// </summary>

    public class AIHeuristic
    {
        //---------- Heuristic PARAMS -------------

        public int board_card_value = 15;       //Score of having cards on board
        public int hand_card_value = 5;         //Score of having cards in hand
        public int kill_value = 10;              //Score of killing a card
        public int empty_board_value = 40;      //Score penalty of an empty board

        public int player_hp_value = 5;         //Score per player hp
        public int card_attack_value = 2;       //Score per board card attack
        public int card_defense_value = 2;       //Score per board card defense
        public int card_agility_value = 1;       //Score per board card agility
        public int card_hp_value = 2;           //Score per board card hp
        public int card_mp_value = 1;           //Score per board card mp
        public int card_status_value = 5;       //Score per status on card (multiplied by hvalue of StatusData)
        public int card_level_value = 1;        //Score per level of cards


        //-----------

        private int ai_player_id;           //ID of this AI, usually the human is 0 and AI is 1
        private int ai_level;               //ai level (level 10 is the best, level 1 is the worst)
        private int heuristic_modifier;     //Randomize heuristic for lower level ai
        private System.Random random_gen;

        public AIHeuristic(int player_id, int level)
        {
            ai_player_id = player_id;
            ai_level = level;
            heuristic_modifier = GetHeuristicModifier();
            random_gen = new System.Random();
        }


        public int CalculateHeuristic(Game data, NodeState node, GameLogic game_logic)
        {
            Player aiplayer = data.GetPlayer(ai_player_id);
            Player oplayer = data.GetOpponentPlayer(ai_player_id);
            return CalculateHeuristic(data, node, aiplayer, oplayer, game_logic);
        }

        //Calculate full heuristic LEGACY LOGIC
        //Should return a value between -10000 and 10000 (unless its a win)
        public int CalculateHeuristicLegacy(Game data, NodeState node, Player aiplayer, Player oplayer, GameLogic game_logic)
        {
            int score = 0;

            //Victories
            if (aiplayer.IsDead())
                score += -100000 + node.tdepth * 2000; //Add node depth to seek surviving longest
            if (oplayer.IsDead())
                score += 100000 - node.tdepth * 2000; //Reduce node depth to seek fastest win

            //Board state
            score += (aiplayer.cards_board.Count * board_card_value) + 10;
            score += aiplayer.cards_hand.Count * hand_card_value;
            score += aiplayer.kill_count * kill_value;
            score += aiplayer.hp * player_hp_value;

            if(aiplayer.cards_board.Count == 0)
            {
                score -= empty_board_value;
            }

            score -= oplayer.cards_board.Count * board_card_value;
            score -= oplayer.cards_hand.Count * hand_card_value;
            score -= oplayer.kill_count * kill_value;
            score -= oplayer.hp * player_hp_value;

            if (oplayer.cards_board.Count == 0)
            {
                score -= empty_board_value / 2;
            }

            foreach (Card card in aiplayer.cards_board)
            {
                score += card.GetAttack() * card_attack_value;
                score += card.GetDefense() * card_defense_value;
                score += card.GetAgility() * card_agility_value;
                score += card.GetHP() * card_hp_value;
                score += card.GetMP() * card_mp_value;

                if (card.CardData.subtype == CardSubType.Summoner)
                {
                    score += 5;
                }
                if (card.CardData.subtype == CardSubType.Fusion)
                {
                    score += 40;
                }


                foreach (Card ocard in oplayer.cards_board)
                {
                    if (card.CardData.type == CardType.Character && ocard.CardData.type == CardType.Character)
                    {
                        int damageModifierScore = 0;

                        // Add score for outgoing affinity damage
                        damageModifierScore += game_logic.AdjustDamageForAffinities(card, ocard, 5, card.CardData.primaryElement);

                        // Reduce score for incoming affinity damage
                        damageModifierScore -= game_logic.AdjustDamageForAffinities(ocard, card, 5, card.CardData.primaryElement);

                        score += damageModifierScore;
                    }
                }

                foreach (CardStatus status in card.status)
                {
                    score += status.StatusData.hvalue * card_status_value;
                }

                foreach (CardStatus status in card.ongoing_status)
                {
                    score += status.StatusData.hvalue * card_status_value;
                }

            }
            foreach (Card card in oplayer.cards_board)
            {
                score -= card.GetAttack() * card_attack_value;
                score -= card.GetDefense() * card_defense_value;
                score -= card.GetAgility() * card_agility_value;
                score -= card.GetHP() * card_hp_value;
                score -= card.GetMP() * card_mp_value;
                
                if(card.CardData.subtype == CardSubType.Summoner)
                {
                    score -= 5;
                }
                if (card.CardData.subtype == CardSubType.Fusion)
                {
                    score -= 25;
                }

                foreach (CardStatus status in card.status)
                    score -= status.StatusData.hvalue * card_status_value;
                foreach (CardStatus status in card.ongoing_status)
                    score -= status.StatusData.hvalue * card_status_value;
            }


            foreach (Card card in aiplayer.cards_hand)
            {
                if (card.CardData.IsBoardCard())
                {
                    score += card.GetAttack() * card_attack_value / 2;
                    score += card.GetDefense() * card_defense_value / 2;
                    score += card.GetAgility() * card_agility_value / 2;
                    score += card.GetHP() * card_hp_value / 2;
                    score += card.GetMP() * card_mp_value / 2;

                    foreach (Card ocard in oplayer.cards_board)
                    {
                        if (card.CardData.type == CardType.Character && ocard.CardData.type == CardType.Character)
                        {
                            int damageModifierScore = 0;

                            // Add score for outgoing affinity damage
                            damageModifierScore += game_logic.AdjustDamageForAffinities(card, ocard, 5, card.CardData.primaryElement);

                            // Reduce score for incoming affinity damage
                            damageModifierScore -= game_logic.AdjustDamageForAffinities(ocard, card, 5, card.CardData.primaryElement);

                            score += damageModifierScore / 2;
                        }
                    }

                    foreach (CardStatus status in card.status)
                    {
                        score += status.StatusData.hvalue * card_status_value / 2;
                    }

                    foreach (CardStatus status in card.ongoing_status)
                    {
                        score += status.StatusData.hvalue * card_status_value / 2;
                    }
                }
            }
            foreach (Card card in oplayer.cards_hand)
            {
                if(card.CardData.IsBoardCard())
                {
                    score -= card.GetAttack() * card_attack_value / 2;
                    score -= card.GetDefense() * card_defense_value / 2;
                    score -= card.GetAgility() * card_agility_value / 2;
                    score -= card.GetHP() * card_hp_value / 2;
                    score -= card.GetMP() * card_mp_value / 2;

                    foreach (CardStatus status in card.status)
                        score -= status.StatusData.hvalue * card_status_value / 2;
                    foreach (CardStatus status in card.ongoing_status)
                        score -= status.StatusData.hvalue * card_status_value / 2;
                }
            }

            return score;
        }

        public int CalculateHeuristic(Game data, NodeState node, Player aiplayer, Player oplayer, GameLogic game_logic)
        {
            int score = 0;

            // Victories
            if (aiplayer.IsDead())
                return int.MinValue + node.tdepth;
            if (oplayer.IsDead())
                return int.MaxValue - node.tdepth;

            // First Turn Evaluation
            score += EvaluateFirstTurns(aiplayer, data);

            // Board state
            score += EvaluatePlayerState(aiplayer, oplayer, true, data, game_logic);
            score -= EvaluatePlayerState(oplayer, aiplayer, false, data, game_logic);

            // Hand potential
            score += EvaluateHandPotential(aiplayer, game_logic);
            score -= EvaluateHandPotential(oplayer, game_logic);

            // Board control
            score += EvaluateBoardControl(aiplayer, oplayer);

            // Consider game phase
            score += EvaluateGamePhase(data, aiplayer, oplayer);

            // Evaluate potential threats and opportunities
            score += EvaluateThreatsAndOpportunities(aiplayer, oplayer, data, game_logic);

            foreach (Card card in aiplayer.cards_board)
            {
                score += EvaluateFusionResult(card, aiplayer, oplayer, game_logic);
            }

            return score;
        }

        private int EvaluateFirstTurns(Player player, Game data)
        {
            int score = 0;

            if (data.turn_count == 1 || data.turn_count == 2)
            {
                // Heavily penalize having no units on board
                if (player.cards_board.Count == 0)
                {
                    score -= 1000;
                }

                // Reward for each unit played on the first turn
                score += player.cards_board.Count * 200;

                // Additional reward for playing a Summoner on the first turn
                if (player.cards_board.Any(c => c.CardData.subtype == CardSubType.Summoner))
                {
                    score += 300;
                }
            }

            return score;
        }

        private int EvaluatePlayerState(Player player, Player opponent, bool isAI, Game data, GameLogic game_logic)
        {
            int score = 0;

            score += player.cards_board.Count * board_card_value;
            if (player.cards_board.Count == 0)
                score -= isAI ? empty_board_value : empty_board_value / 2;

            score += player.kill_count * kill_value;
            score += player.hp * player_hp_value;

            foreach (Card card in player.cards_board)
            {
                score += EvaluateCard(card, isAI, opponent, data, game_logic);
                score += EvaluateStatusEffectPotential(card, game_logic);
                score += EvaluateActionPotential(card);
                score += EvaluateSpecialCards(card);
            }

            return score;
        }

        private int EvaluateCard(Card card, bool isAI, Player opponent, Game data, GameLogic game_logic)
        {
            int score = 0;

            score += card.GetAttack() * card_attack_value;
            score += card.GetDefense() * card_defense_value;
            score += card.GetHP() * card_hp_value;
            score += card.GetMP() * card_mp_value;
            score += card.GetAgility() * card_agility_value;

            foreach (Card ocard in opponent.cards_board)
            {
                if (card.CardData.type == CardType.Character && ocard.CardData.type == CardType.Character)
                {
                    int affinityScore = game_logic.AdjustDamageForAffinities(card, ocard, 5, card.CardData.primaryElement);
                    affinityScore -= game_logic.AdjustDamageForAffinities(ocard, card, 5, card.CardData.primaryElement);
                    score += affinityScore;
                }
            }

            foreach (CardStatus status in card.status.Concat(card.ongoing_status))
            {
                score += status.StatusData.hvalue * card_status_value;
            }

            return score;
        }

        private int EvaluateStatusEffectPotential(Card card, GameLogic game_logic)
        {
            int score = 0;
            foreach (CardStatus status in card.status)
            {
                if (GameLogic.dotValues.ContainsKey(status.type))
                {
                    score += GameLogic.CalculateDOTDamage(card, status.type);
                }
            }
            return score;
        }

        private int EvaluateActionPotential(Card card)
        {
            return (card.availableActions * 10) + (card.availableHalfActions * 5);
        }

        private int EvaluateSpecialCards(Card card)
        {
            int score = 0;
            if (card.CardData.subtype == CardSubType.Fusion) score += 30;
            if (card.CardData.subtype == CardSubType.Summoner) score += 20;
            return score;
        }

        private int EvaluateHandPotential(Player player, GameLogic game_logic)
        {
            int score = 0;
            score += player.cards_hand.Count * hand_card_value;

            foreach (Card card in player.cards_hand)
            {
                if (card.CardData.IsBoardCard())
                {
                    score += EvaluateCard(card, player == game_logic.GameData.GetPlayer(game_logic.GameData.current_player),
                                          game_logic.GameData.GetOpponentPlayer(player.player_id), game_logic.GameData, game_logic) / 2;
                }
            }

            return score;
        }

        private int EvaluateBoardControl(Player player, Player opponent)
        {
            int score = 0;
            score += player.cards_board.Count * 10;
            score -= opponent.cards_board.Count * 10;
            score += player.GetEmptySlots().Count() * 5;
            return score;
        }

        private int EvaluateGamePhase(Game data, Player aiplayer, Player oplayer)
        {
            int score = 0;
            int turnCount = data.turn_count;

            // Early game (first 4 turns)
            if (turnCount <= 4)
            {
                // Heavily prioritize board presence
                score += aiplayer.cards_board.Count * 100;

                // Severe penalty for empty board
                if (aiplayer.cards_board.Count == 0)
                    score -= 500;

                // Still value card advantage, but less than board presence
                score += aiplayer.cards_hand.Count * 10;

                // Bonus for having units in hand to play
                score += aiplayer.cards_hand.Count(c => c.CardData.IsBoardCard()) * 20;
            }
            // Mid game (turns 4-8)
            else if (turnCount <= 8)
            {
                // Continue to value board presence
                score += aiplayer.cards_board.Count * 20;

                // Card advantage becomes more important
                score += aiplayer.cards_hand.Count * 5;

                // Value board advantage
                score += (aiplayer.cards_board.Count - oplayer.cards_board.Count) * 15;
            }
            // Late game (turn 8+)
            else
            {
                // Board presence still important
                score += aiplayer.cards_board.Count * 15;

                // Card advantage for finishing moves
                score += aiplayer.cards_hand.Count * 5;

                // Prioritize damage to opponent
                score += (oplayer.hp_max - oplayer.hp) * 10;

                // Value board advantage
                score += (aiplayer.cards_board.Count - oplayer.cards_board.Count) * 20;
            }

            return score;
        }

        private int EvaluateFusionResult(Card card, Player player, Player opponent, GameLogic game_logic)
        {
            int score = 0;

            if (card.CardData.subtype == CardSubType.Fusion)
            {
                // Base score for having a fusion on the board
                score += 50;

                // Evaluate the fusion card's current state
                score += card.GetAttack() * 3;
                score += card.GetHP() * 2;
                score += card.GetMP() * 2;

                // Evaluate elemental advantage against opponent's board
                foreach (var opponentCard in opponent.cards_board)
                {
                    int advantage = game_logic.AdjustDamageForAffinities(card, opponentCard, 5, card.CardData.primaryElement) - 5;
                    score += advantage * 5;
                }

                // Evaluate unique abilities
                score += EvaluateUniqueAbilities(card, player, opponent, game_logic) * 10;

                // Consider the current game phase
                int turnCount = game_logic.GameData.turn_count;
                if (turnCount <= 3)
                    score += 30; // Early game fusion is very valuable
                else if (turnCount <= 7)
                    score += 20; // Mid game fusion is still good
                else
                    score += 10; // Late game fusion is less impactful, but still positive

                // Consider board state
                if (player.cards_board.Count < opponent.cards_board.Count)
                    score += 40; // Fusion helping to catch up on board presence
            }

            return score;
        }

        private int EvaluateUniqueAbilities(Card card, Player player, Player opponent, GameLogic game_logic)
        {
            int value = 0;
            foreach (var ability in card.GetAbilities())
            {
                if (ability.trigger == AbilityTrigger.OnPlay)
                    value += 5;
                if (ability.trigger == AbilityTrigger.OnDeath)
                    value += 3;
            }
            return value;
        }

        private int EvaluateThreatsAndOpportunities(Player aiplayer, Player oplayer, Game data, GameLogic game_logic)
        {
            int score = 0;

            foreach (Card ocard in oplayer.cards_board)
            {
                if (game_logic.GameData.CanAttackTarget(ocard, aiplayer) && ocard.GetAttack() > aiplayer.hp)
                {
                    score -= 1000; // Huge penalty for potential lethal threat
                }
            }

            int potentialDamage = aiplayer.cards_board.Where(c => game_logic.GameData.CanAttackTarget(c, oplayer)).Sum(c => c.GetAttack());
            if (potentialDamage >= oplayer.hp)
            {
                score += 1000; // Huge bonus for potential lethal
            }

            return score;
        }
        //This calculates the score of an individual action, instead of the board state
        //When too many actions are possible in a single node, only the ones with best action score will be evaluated
        //Make sure to return a positive value
        public int CalculateActionScore(Game data, AIAction order)
        {
            if (order.type == GameAction.EndTurn)
                return 0; //Other orders are better

            if (order.type == GameAction.CancelSelect)
                return 0; //Other orders are better

            if (order.type == GameAction.CastAbility)
            {
                return 200;
            }

            if (order.type == GameAction.Attack)
            {
                Card card = data.GetCard(order.card_uid);
                Card target = data.GetCard(order.target_uid);
               
                int ascore = card.GetAttack() >= target.GetHP() ? 300 : 100; //Are you killing the card?
                int oscore = target.GetAttack() >= card.GetHP() ? -200 : 0; //Are you getting killed?
                return ascore + oscore + target.GetAttack() * 5;            //Always better to get rid of high-attack cards
            }
            if (order.type == GameAction.AttackPlayer)
            {
                Card card = data.GetCard(order.card_uid);
                Player player = data.GetPlayer(order.target_player_id);
                int ascore = card.GetAttack() >= player.hp ? 500 : 200;     //Are you killing the player?
                return ascore + (card.GetAttack() * 10) - player.hp;        //Always better to inflict more damage
            }
            if (order.type == GameAction.PlayCard)
            {
                Player player = data.GetPlayer(ai_player_id);
                Card card = data.GetCard(order.card_uid);
                if (card.CardData.IsBoardCard())
                    return 200 + (card.GetMana() * 5) - (30 * player.cards_board.Count); //High cost cards are better to play, better to play when not a lot of cards in play
                else
                    return 200 + (card.GetMana() * 5);
            }

            if (order.type == GameAction.Move)
            {
                return 0;
            }

            return 100; //Other actions are better than End/Cancel
        }

        //Within the same turn, actions can only be executed in sorting order, make sure it returns positive value higher than 0 or it wont be sorted
        //This prevents calculating all possibilities of A->B->C  B->C->A   C->A->B  etc..
        //If two AIActions with same sorting value, or if sorting value is 0, ai will test all ordering variations (slower)
        //This would not be necessary in a game with only 1 action per turn (such as chess) but is useful for AI that can perform multiple actions in 1 turn
        //Ordering could be improved, pretty much random now
        public int CalculateActionSort(Game data, AIAction order)
        {
            if (order.type == GameAction.EndTurn)
                return 10; //End turn can always be performed, 0 means any order
            if (data.selector != SelectorType.None)
                return 0; //Selector actions not affected by sorting

            Card card = data.GetCard(order.card_uid);
            Card target = order.target_uid != null ? data.GetCard(order.target_uid) : null;
            bool is_spell = card != null && !card.CardData.IsBoardCard();

            int type_sort = 0;

            if (order.type == GameAction.PlayCard && !is_spell)
                type_sort = 1;
            if (order.type == GameAction.PlayCard && is_spell)
                type_sort = 2;
            if (order.type == GameAction.AttackPlayer)
                type_sort = 3;
            if (order.type == GameAction.CastAbility)
                type_sort = 3;
            if (order.type == GameAction.Attack)
                type_sort = 3; 

            int card_sort = card != null ? (card.Hash % 100) : 0;
            int target_sort = target != null ? (target.Hash % 100) : 0;
            int sort = type_sort * 10000 + card_sort * 100 + target_sort + 1;
            return sort;
        }

        //Lower level AI add a random number to their heuristic
        private int GetHeuristicModifier()
        {
            return 0;
        }

        //Check if this node represent one of the players winning
        public bool IsWin(NodeState node)
        {
            return node.hvalue > 50000 || node.hvalue < -50000;
        }

    }
}
