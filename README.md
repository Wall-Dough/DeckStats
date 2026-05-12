# DeckStats

Inspired by the deck composition stats in Balatro, DeckStats provides a quick and simple overview of the contents of your deck. Card "categories" are listed with a count and a percentage. I made this mod a month ago and have been using it almost every day. It is, no pun intended, a game changer. Where it truly shines for me is the Block percentage, I'm always checking that to quickly decide whether I have enough Block or not. It isn't a perfect metric for Block in this game, but it really helps.

DeckStats is visible in the Deck View Screen at all times during the run. It is also available in the Draw Pile View Screen, and the Discard Pile View Screen, although for the Discard Pile, it is minimized by default. In all locations, can toggle its visibility by clicking the name bar.

## Available Stats

Currently, the stats are statically arranged in this way. I would love to allow arranging them how you like but I haven't been able to figure that out yet.

| Total   |               |            |
|---------|---------------|------------|
| Attacks | Single Target | Block      |
| Skills  | AOE           | Weak       |
| Powers  | Random Enemy  | Vulnerable |
| Curses  |               | Card Draw  |
| Quests  |               | Ethereal   |

## ModConfig support

I haven't tested it in a few patches but there is loose [ModConfig](https://www.nexusmods.com/slaythespire2/mods/27) support, all of the stats can be toggled on or off. Though the mod still works if you do not have ModConfig installed.

## "Second Cycle" Stats

There is checkbox in the bottom right labeled "Second Cycle", which changes DeckStats for the current screen to show the stats for your deck after the first draw cycle. It gives you the stats after you've played all Powers, played all cards that Exhaust themselves, and allowed all Ethereal Curses (e.g. Ascender's Bane) to Exhaust. Another example of an imperfect metric that's still powerful (in my opinion). It can be nice to see to understand how your longer fights will fare.

## Other considerations

Just some minor thoughts that I've had for a while now:

- I've considered having contextual stats like character specific stuff, e.g. Star Cost cards and Star Gain cards for The Regent, Summon cards for The Necrobinder. Haven't decided how to do that yet but it's in the back of my mind.
- Summon can be considered Block, but for now I don't consider it Block. There are exceptions where Summon isn't Block, and trying to determine how to reconcile that is too much.
  -  I'm now thinking there could be "Offensive" cards (Attacks, HP Loss) and "Defensive" cards (Block, Summon, Weak, Strength-), but still, the categorization there is too fuzzy. Most of the stats here are directly tied to an in-game property of cards, which is way more concrete.
