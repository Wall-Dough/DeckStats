using System.Collections;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace DeckStats.DeckStatsCode;

public static class DeckStats
{
    private static bool _configLoaded = false;
    public static string NONE = "(None)";
    public static string TOTAL = "Total";
    public static string ATTACKS = "Attacks";
    public static string SKILLS = "Skills";
    public static string POWERS = "Powers";
    public static string CURSES = "Curses";
    public static string QUESTS = "Quests";
    public static string SINGLE_TARGET = "Single_Target";
    public static string AOE = "AOE";
    public static string RANDOM_ENEMY = "Random_Enemy";
    public static string BLOCK = "Block";
    public static string WEAK = "Weak";
    public static string VULNERABLE = "Vulnerable";
    public static string CARD_DRAW = "Card_Draw";
    public static string ETHEREAL = "Ethereal";

    public static string[] ALL_STATS =
    [
        NONE,
        TOTAL, ATTACKS, SKILLS, POWERS, CURSES, QUESTS,
        SINGLE_TARGET, AOE, RANDOM_ENEMY,
        BLOCK, WEAK, VULNERABLE, CARD_DRAW, ETHEREAL
    ];

    public static string[][] COLUMNS =
    [
        [TOTAL, ATTACKS,       SKILLS, POWERS,       CURSES,    QUESTS],
        [NONE,  SINGLE_TARGET, AOE,    RANDOM_ENEMY],
        [NONE,  BLOCK,         WEAK,   VULNERABLE,   CARD_DRAW, ETHEREAL]
    ];

    public static string[][] currentColumns = COLUMNS;
    
    public static Hashtable showStats = new();
    public static Hashtable statValues = new();

    public static void LoadConfig(bool forceLoad = false)
    {
        if (!ModConfigBridge.IsAvailable)
        {
            MainFile.Logger.Warn("ModConfigBridge unavailable, skipping");
            
            return;
        }
        if (_configLoaded && !forceLoad)
        {
            MainFile.Logger.Warn("Config already loaded, skipping");
            
            return;
        }
        showStats.Clear();
        foreach (string statName in ALL_STATS)
        {
            if (statName == NONE) continue;

            showStats[statName] = ModConfigBridge.GetValue(statName, true);
        }
        UpdateColumns();
    }

    public static void ShowStat(string statName, bool show)
    {
        showStats[statName] = show;
    }

    public static bool GetShowStat(string statName)
    {
        return !showStats.ContainsKey(statName) || showStats[statName] is true;
    }

    public static void UpdateColumns()
    {
        string[][] newColumns = new string[COLUMNS.GetUpperBound(0) + 1][];

        int numColumns = 0;
        foreach (string[] column in COLUMNS)
        {
            string[] newColumn = new string[column.GetUpperBound(0) + 1];
            int numRows = 0;
            foreach (string statName in column)
            {
                if (GetShowStat(statName))
                {
                    newColumn[numRows++] = statName;
                }
            }
            newColumns[numColumns++] = newColumn;
        }
        
        currentColumns = newColumns;
    }

    public static string GetStatTableCell(int rowNum, int colNum)
    {
        if (colNum < currentColumns.GetLowerBound(0) || colNum > currentColumns.GetUpperBound(0))
        {
            return NONE;
        }
        string[] column = currentColumns[colNum];
        if (rowNum < column.GetLowerBound(0) || rowNum > column.GetUpperBound(0))
        {
            return NONE;
        }
        return column[rowNum];
    }

    public static int GetStatValue(string statName)
    {
        if (statValues.ContainsKey(statName))
        {
            object? value = statValues[statName];
            if (value != null && value is int)
            {
                return (int) value;
            }
        }
        return -1;
    }

    public static int GetStatTableWidth()
    {
        return currentColumns.GetUpperBound(0) + 1;
    }

    public static int GetStatTableHeight()
    {
        MainFile.Logger.Info(currentColumns.ToString());
        if (currentColumns == null)
        {
            MainFile.Logger.Warn("null current columns");
        }
        int maxLength = 0;
        foreach (string[] column in currentColumns)
        {
            if (column == null)
            {
                MainFile.Logger.Warn("null column");
            }
            if (column.Length > maxLength)
            {
                maxLength = column.Length;
            }
        }
        return maxLength;
    }

    public static void CalculateDeckStats(IReadOnlyList<CardModel> cards)
    {
        statValues.Clear();
        if (cards.Count == 0)
        {
            return;
        }
        statValues.Add(TOTAL, cards.Count);
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
        int numCardDraw = 0;
        int numEthereal = 0;
        foreach (CardModel card in cards)
        {
            try
            {
                if (card.Type == CardType.Attack)
                {
                    numAttacks++;
                }

                // TODO: get more specific with these?
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

                if (card.DynamicVars.ContainsKey("Cards") || card.DynamicVars.ContainsKey("DrawCardsNextTurnPower"))
                {
                    numCardDraw++;
                }

                if (card.CanonicalKeywords.Contains(CardKeyword.Ethereal))
                {
                    numEthereal++;
                }
            }
            catch (Exception e)
            {
                MainFile.AddException(e);
            }
        }

        statValues.Add(ATTACKS, numAttacks);
        statValues.Add(SKILLS, numSkills);
        statValues.Add(POWERS, numPowers);    
        statValues.Add(CURSES, numCurses);
        statValues.Add(QUESTS, numQuests);
        statValues.Add(SINGLE_TARGET, numSingleTarget);
        statValues.Add(AOE, numAOE);
        statValues.Add(RANDOM_ENEMY, numRandom);
        statValues.Add(BLOCK, numBlock);
        statValues.Add(WEAK, numWeak);
        statValues.Add(VULNERABLE, numVulnerable);
        statValues.Add(CARD_DRAW, numCardDraw);
        statValues.Add(ETHEREAL, numEthereal);
    }

    public static int GetTotalCardCount()
    {
        if (statValues.ContainsKey(TOTAL))
        {
            return (int) statValues[TOTAL];
        }

        return 0;
    }
}
