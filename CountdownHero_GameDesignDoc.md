# Countdown Hero - One-Page Design Doc

## High Concept

A darkly comedic platformer-RPG where every action depends on a countdown. The hero is cursed with `Countdown Syndrome`, a magical condition that forces attacks, defenses, and even parts of the level itself to obey timed signals.

## Player Fantasy

Move like a precise platformer hero, then survive battles where timing matters more than menu choices. The player is always watching for the real `Go!` moment.

## Core Loop

1. Explore as a side-scrolling platformer.
2. Enter a marked encounter zone.
3. Switch into countdown-based battle.
4. Return to platforming after the fight.
5. Repeat until the boss.

## Platforming

The movement should feel tight and readable, with familiar platformer basics:

- left/right movement
- jump
- coyote time
- variable jump height

Some level pieces also use countdowns. Platforms may appear, disappear, or become usable only after a delay, creating timed traversal puzzles and keeping the theme present outside combat.

## Battle System

Battles are turn-based, but the player does not pick from a traditional menu. Each action is tied to a countdown.

Example:

- Player attack: `3, 2, 1, Go!`
- Enemy attack: `3, 2, 1, Go!`

The twist is that the countdown is sometimes weird or misleading:

- `3 ... 2 1 Go!`
- `3, 2 ... , 1, Go!`
- `3, 3, 3, 3, 2, 2, 1, Go!`

The player must stay focused and react to the actual signal instead of assuming the rhythm will stay honest.

## Story

As a baby, the main character was cursed by a witch for no sensible reason beyond her being completely unhinged. The curse gave him `Countdown Syndrome`, which makes him unable to act freely without a countdown first.

His goal is simple: travel through the world, survive the curse, find the witch, and break the spell.

## Tone

The game is dark fantasy with comedy. The premise is serious enough to care about, but the witch and the curse are absurd enough to keep the whole thing weird and memorable.

## MVP For The Jam

Must-have:

- responsive movement
- jump with coyote time and variable height
- encounter zones that trigger battle
- countdown-based attack
- countdown-based defense
- at least one timed platform gimmick
- short intro that explains `Countdown Syndrome`

Should-have:

- one or two enemy patterns
- a few weird countdown variations
- basic countdown UI
- simple win/lose states

Only if there is time:

- boss fight
- extra platforming mechanics
- more enemy types
- stronger story presentation
- extra polish like sound, screenshake, and animation flair

## One-Sentence Pitch

A cursed platformer-RPG where every action depends on a bizarre countdown, and a boy with `Countdown Syndrome` fights through timed battles and shifting platforming challenges to hunt down the witch who cursed him.

