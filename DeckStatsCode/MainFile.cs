using System.Diagnostics;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
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
        ModConfigBridge.DeferredRegister();
    }

    public static void AddException(Exception e)
    {
        DeckStatsPatch.AddException(e);
    }

    [HarmonyPatch]
    public class DeckStatsPatch
    {
        private static string _name = "DeckStats";
        private static PanelContainer? _container;
        private static RichTextLabel? _label;
        private static PileType? _lastPileType;
        private static Vector2? _cardSize;
        private static Font? _regularFont;
        private static Font? _boldFont;
        private static int _regularFontSize = 0;
        private static int _boldFontSize = 0;
        public static List<Exception> Exceptions = new();

        public static void AddException(Exception e)
        {
            Exceptions.Add(e);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NDeckViewScreen))]
        [HarmonyPatch("DisplayCards")]
        private static void BeforeDisplayCards(NDeckViewScreen __instance)
        {
            if (_container != null && __instance.HasNode(new NodePath(_name)))
            {
                __instance.RemoveChild(_container);
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
            List<SortingOrders> sortingPriority, Task? taskToWaitOn)
        {
            _lastPileType = pileType;
            if (pileType != PileType.Deck)
            {
                return;
            }
            
            DeckStats.CalculateDeckStats(cardsToDisplay);
        }

        private static void CreateDeckStatsNode()
        {
            _container = new PanelContainer();
            _container.SetName(_name);
            StyleBox panelStyleBox = (StyleBox) _container.GetThemeStylebox(new StringName("panel")).Duplicate();
            panelStyleBox.Set(new StringName("bg_color"), new Color(Colors.Black, 0.75f));
            _container.AddThemeStyleboxOverride(new StringName("panel"), panelStyleBox);
            _label = new RichTextLabel();
            _label.SetFitContent(true);
            if (_regularFont != null && _boldFont != null)
            {
                _label.AddThemeFontOverride(ThemeConstants.RichTextLabel.NormalFont, _regularFont);
                _label.AddThemeFontOverride(ThemeConstants.RichTextLabel.BoldFont, _boldFont);
                _label.AddThemeColorOverride(ThemeConstants.RichTextLabel.FontShadowColor, Colors.Black);
                _label.AddThemeFontSizeOverride(ThemeConstants.RichTextLabel.NormalFontSize, _regularFontSize);
                _label.AddThemeFontSizeOverride(ThemeConstants.RichTextLabel.BoldFontSize, _boldFontSize);
            }
            else
            {
                Logger.Warn("Could not find font files");
            }
            _label.SetAutowrapMode(TextServer.AutowrapMode.Off);
            _container.AddChild(_label);
        }

        private static void PopulateDeckStatsLabel()
        {
            if (_label == null)
            {
                CreateDeckStatsNode();
            }
            if (_label == null)
            {
                Logger.Error("Null DeckStats label");
                return;
            }

            DeckStats.LoadConfig();

            _label.Clear();

            int tableWidth = DeckStats.GetStatTableWidth();
            int tableHeight = DeckStats.GetStatTableHeight();
            _label.PushTable(tableWidth * 3);
            Rect2 cellPadding = new Rect2(10, 0, 10, 0);
            int totalCards = DeckStats.GetTotalCardCount();
            for (int rowNum = 0; rowNum < tableHeight; rowNum++)
            {
                for (int colNum = 0; colNum < tableWidth; colNum++)
                {
                    string statName = DeckStats.GetStatTableCell(rowNum, colNum);
                    int statValue = DeckStats.GetStatValue(statName);
                    if (statName == DeckStats.NONE || statValue < 0)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            _label.PushCell();
                            _label.SetCellPadding(cellPadding);
                            _label.Pop();
                        }
                        continue;
                    }
                    int percent = (int) ((float) statValue / totalCards * 100);
                    _label.PushCell();
                    _label.SetCellPadding(cellPadding);
                    _label.AppendText("[b]" + statName.Replace('_', ' ') + ":[/b]");
                    _label.Pop();
                    _label.PushCell();
                    _label.SetCellPadding(cellPadding);
                    _label.AppendText(statValue.ToString());
                    _label.Pop();
                    _label.PushCell();
                    _label.SetCellPadding(cellPadding);
                    _label.AppendText("(" + percent + "%)");
                    _label.Pop();
                }
            }
            _label.Pop();
            
            Logger.Info(_label.GetText());
        }

        private static void LogAllChildren(Node parent)
        {
            Logger.Info("All child nodes of:" + parent.GetPath());
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
            Control viewUpgrades = __instance.GetNode<Control>("ViewUpgrades");
            Control bottomText = __instance.GetNode<Control>("BottomText");
            Vector2 bottomTextPosition = new Vector2(bottomText.GetPosition().X,
                viewUpgrades.GetPosition().Y + viewUpgrades.GetSize().Y - bottomText.GetSize().Y);
            bottomText.SetPosition(bottomTextPosition);
            
            if (_regularFont == null || _boldFont == null)
            {
                MegaRichTextLabel bottomLabel = bottomText.GetNode<MegaRichTextLabel>("MarginContainer/BottomLabel");
                _regularFont = bottomLabel.GetThemeFont(ThemeConstants.RichTextLabel.NormalFont);
                _boldFont = bottomLabel.GetThemeFont(ThemeConstants.RichTextLabel.BoldFont);
                _regularFontSize = bottomLabel.GetThemeFontSize(ThemeConstants.RichTextLabel.NormalFontSize);
                _boldFontSize = bottomLabel.GetThemeFontSize(ThemeConstants.RichTextLabel.BoldFontSize);
            }
            
            if (_lastPileType != PileType.Deck)
            {
                return;
            }
            
            if (!__instance.HasNode(new NodePath(_name)))
            {
                if (_container == null)
                {
                    CreateDeckStatsNode();
                    PopulateDeckStatsLabel();
                }

                if (_label == null)
                {
                    Logger.Error("Null DeckStats label");
                    return;
                }

                if (_container == null)
                {
                    Logger.Error("Null DeckStats container");
                    return;
                }

                __instance.AddChild(_container);
                Vector2 position = new Vector2(viewUpgrades.GetPosition().X + viewUpgrades.GetSize().X,
                    viewUpgrades.GetPosition().Y + viewUpgrades.GetSize().Y - _label.GetContentHeight());
                float containerWidth = 300;
                if (_cardSize == null)
                {
                    Logger.Warn("Null DeckStats card size");
                }
                else
                {
                    containerWidth = _cardSize.Value.X;
                }
                _container.SetPosition(position);
                _container.SetSize(new Vector2(containerWidth, _label.GetContentHeight()));
                bottomTextPosition = new Vector2(bottomText.GetPosition().X, position.Y - bottomText.GetSize().Y - 10);
                bottomText.SetPosition(bottomTextPosition);
            }
        }
    }
}