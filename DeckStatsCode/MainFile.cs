using System.Collections;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace DeckStats.DeckStatsCode;

//You're recommended but not required to keep all your code in this package and all your assets in the DeckStats folder.
[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "DeckStats"; //At the moment, this is used only for the Logger and harmony names.

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Harmony harmony = new(ModId);

        harmony.PatchAll();
    }

    [HarmonyPatch]
    public class DeckStatsPatch
    {
        private static string _name = "DeckStats";
        private static Control? _container;
        private static RichTextLabel? _label;
        private static Hashtable _deckStats = new();
        private static PileType? _lastPileType;
        private static Vector2? _cardSize;

        private enum StatName
        {
            Total = 0,
            Attacks = 1,
            Skills = 2,
            Powers = 3,
            Curses = 4,
            Quests = 5,
            Single_Target = 6,
            AOE = 7,
            Random_Enemy = 8,
            Block = 9,
            Weak = 10,
            Vulnerable = 11
        }

        private static string?[][] _statOrder = [
            [nameof(StatName.Total), null, null],
            [nameof(StatName.Attacks), nameof(StatName.Single_Target), nameof(StatName.Block)],
            [nameof(StatName.Skills), nameof(StatName.AOE), nameof(StatName.Weak)],
            [nameof(StatName.Powers), nameof(StatName.Random_Enemy), nameof(StatName.Vulnerable)],
            [nameof(StatName.Curses), null, null],
            [nameof(StatName.Quests), null, null]
        ];

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NDeckViewScreen))]
        [HarmonyPatch("DisplayCards")]
        private static void BeforeDisplayCards(NDeckViewScreen __instance)
        {
            Node parent = __instance.GetNode(new NodePath("CardGrid/ScrollContainer"));
            if (_container != null && parent.HasNode(new NodePath(_name)))
            {
                parent.RemoveChild(_container);
                _container = null;
                _label = null;
            }

            if (_container != null || _label != null)
            {
                _container = null;
                _label = null;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NCardGrid))]
        [HarmonyPatch("GetContainedCardsSize")]
        private static void AfterGetContainedCardsSize(ref Vector2 __result)
        {
            _cardSize = __result;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NCardGrid))]
        [HarmonyPatch("SetCards")]
        private static void BeforeSetCards(IReadOnlyList<CardModel> cardsToDisplay, PileType pileType,
            List<SortingOrders> sortingPriority, Task? taskToWaitOn, NCardGrid __instance)
        {
            _lastPileType = pileType;
            if (pileType != PileType.Deck)
            {
                return;
            }
            _deckStats.Clear();
            if (cardsToDisplay.Count == 0)
            {
                return;
            }
            _deckStats.Add(nameof(StatName.Total), cardsToDisplay.Count);
            int numAttacks = 0;
            int numSingleTarget = 0;
            int numAOE = 0;
            int numRandom = 0;
            int numSkills = 0;
            int numPowers = 0;
            int numCurses = 0;
            int numQuests = 0;
            int numBlock = 0;
            int numWeak = 0;
            int numVulnerable = 0;
            foreach (CardModel card in cardsToDisplay)
            {
                if (card.Type == CardType.Attack)
                {
                    numAttacks++;
                    if (card.TargetType == TargetType.AnyEnemy)
                    {
                        numSingleTarget++;
                    }
                    if (card.TargetType == TargetType.AllEnemies)
                    {
                        numAOE++;
                    }
                    if (card.TargetType == TargetType.RandomEnemy)
                    {
                        numRandom++;
                    }
                }
                if (card.Type == CardType.Skill)
                {
                    numSkills++;
                }
                if (card.Type == CardType.Power)
                {
                    numPowers++;
                }
                if (card.Type == CardType.Curse)
                {
                    numCurses++;
                }
                if (card.Type == CardType.Quest)
                {
                    numQuests++;
                }

                if (card.GainsBlock)
                {
                    numBlock++;
                }

                if (card.DynamicVars.ContainsKey("WeakPower"))
                {
                    numWeak++;
                }
                if (card.DynamicVars.ContainsKey("VulnerablePower"))
                {
                    numVulnerable++;
                }
            }

            _deckStats.Add(nameof(StatName.Attacks), numAttacks);
            _deckStats.Add(nameof(StatName.Skills), numSkills);
            _deckStats.Add(nameof(StatName.Powers), numPowers);    
            _deckStats.Add(nameof(StatName.Curses), numCurses);
            _deckStats.Add(nameof(StatName.Quests), numQuests);
            _deckStats.Add(nameof(StatName.Single_Target), numSingleTarget);
            _deckStats.Add(nameof(StatName.AOE), numAOE);
            _deckStats.Add(nameof(StatName.Random_Enemy), numRandom);
            _deckStats.Add(nameof(StatName.Block), numBlock);
            _deckStats.Add(nameof(StatName.Weak), numWeak);
            _deckStats.Add(nameof(StatName.Vulnerable), numVulnerable);
        }

        private static void CreateDeckStatsNode()
        {
            _container = new Control();
            _container.SetName(_name);
            _label = new RichTextLabel();
            _label.SetFitContent(true);
            _label.SetAutowrapMode(TextServer.AutowrapMode.Off);
            _container.AddChild(_label);
        }

        private static void PopulateDeckStatsLabel()
        {
            if (_label == null)
            {
                CreateDeckStatsNode();
            }
            
            _label.Clear();

            _label.PushTable(_statOrder[0].Length * 3);
            Rect2 cellPadding = new Rect2(10, 0, 10, 0);
            int totalCards = (int) _deckStats[nameof(StatName.Total)];
            foreach (string?[] row in _statOrder)
            {
                foreach (string? statName in row)
                {
                    if (statName == null)
                    {
                        _label.PushCell();
                        _label.Pop();
                        _label.PushCell();
                        _label.Pop();
                        _label.PushCell();
                        _label.Pop();
                        continue;
                    }
                    if (_deckStats.ContainsKey(statName))
                    {
                        int statValue = (int) _deckStats[statName];
                        int percent = (int) ((float) statValue / totalCards * 100);
                        _label.PushCell();
                        _label.SetCellPadding(cellPadding);
                        _label.AppendText("[b]" + statName.Replace('_', ' ') + ":[/b]");
                        _label.Pop();
                        _label.PushCell();
                        _label.SetCellPadding(cellPadding);
                        _label.AppendText(_deckStats[statName].ToString());
                        _label.Pop();
                        _label.PushCell();
                        _label.SetCellPadding(cellPadding);
                        _label.AppendText("(" + percent + "%)");
                        _label.Pop();
                    }
                }
            }
            _label.Pop();
            
            Logger.Info(_label.GetText());
        }

        private static void LogAllChildren(Node parent)
        {
            foreach (Node child in parent.GetChildren())
            {
                Logger.Info("  " + child.GetName() + " (" + child.GetType().Name + ")");
            }
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NDeckViewScreen))]
        [HarmonyPatch("DisplayCards")]
        private static void AfterDisplayCards(NDeckViewScreen __instance)
        {
            if (_lastPileType != PileType.Deck)
            {
                return;
            }
            
            Control parent = __instance.GetNode<Control>(new NodePath("CardGrid/ScrollContainer"));
            LogAllChildren(parent);

            if (!parent.HasNode(new NodePath(_name)))
            {
                if (_container == null)
                {
                    CreateDeckStatsNode();
                    PopulateDeckStatsLabel();
                }
                
                Control sortingOptions = parent.GetNode<Control>("SortingOptions");
                
                parent.AddChild(_container);
                Vector2 position = new Vector2(0, 40 + sortingOptions.GetPosition().Y + sortingOptions.GetSize().Y + _cardSize.Value.Y);
                _container.SetPosition(position);
            }
        }
    }
}