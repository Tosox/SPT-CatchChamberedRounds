# Catch Chambered Rounds

## 📜 Description

**Catch Chambered Rounds** is a mod for *Single Player Tarkov* that stops live rounds from being thrown on the floor when a reload clears the chamber.

The case it exists for is the **Mosin**: hold it in your hands with a round chambered, hit reload, and the game pulls that round out of the chamber and tosses it into the world as loot. In practice it lands under your feet, in a bush, or through the floor, and you never see it again. The same applies to the other rifles that have to open the bolt to top up an internal magazine.

With this mod your character keeps the round instead. It goes into your rig, pockets or backpack exactly like dragging it out of the chamber slot on the inventory screen would, merging into a matching stack when you already have one.

If there is genuinely nowhere to put it, the round drops on the ground as usual, so you never lose one to a full inventory.

Nothing is announced when a round is caught, because that is simply how the mod is meant to behave. Turn on **Notify When A Round Drops** if you would rather be told on the occasions you *do* lose one.

Reloads that cannot achieve anything are also refused. A Mosin holding four rounds in the magazine and one in the chamber is already carrying its full five, so a reload would only pull the chambered round out and feed a replacement in from your gear. The count would not move and you would have paid an animation for it, so the weapon is simply left alone.

## ✨ Features

* Keeps the chambered round when reloading internal magazine weapons (Mosin, SKS, ...)
* Plays the real vanilla catch animation: your character pulls the round out and stows it before working the bolt
* Keep your Quick reload key for when the round is worth less than the second it costs you
* Deliberately narrow: nothing else the game throws on the floor is touched, spent casings included
* Bots are untouched and keep vanilla behaviour

## 📁 Installation

* Download the latest release
* Copy the `BepInEx` folder into your SPT folder
* Start the game

## ⚙️ Configuration

Settings live under `BepInEx/config/de.tosox.catchchamberedrounds.cfg`, or in the F12 menu if you use Configuration Manager.

| Setting | Default | What it does |
| --- | --- | --- |
| **Enabled** | `true` | Master switch |
| **Notify When A Round Drops** | `false` | Tells you which round hit the floor, and whether it was for speed or for lack of space |
| **Play Catch Animation** | `true` | Pull the round out with the vanilla catch animation before reloading. Turn off to keep the round silently with no added delay |
| **Quick Reload Skips The Catch** | `true` | Tarkov's Quick reload key reloads at full speed and drops the round instead of catching it |
| **Invert Reload Keys** | `false` | Swap the two around, so the normal reload is the fast one and Quick reload is the one that catches |
| **Block Pointless Reloads** | `true` | Refuse a reload that cannot add rounds, such as 4 in the magazine with 1 chambered |

## 🎬 A note on the animation

With **Play Catch Animation** on, reloading a Mosin that still has a round chambered makes your character do what you would do by hand: pull the round out of the chamber and stow it, then reload. That first part is the genuine vanilla animation, the same one you get by dragging the round out of the chamber slot on the inventory screen. Nothing is faked or authored.

The cost is time. You are watching two animations back to back instead of one, so the whole thing takes noticeably longer than a vanilla reload. If that feels sluggish, turn **Play Catch Animation** off: the round is still kept, it just happens silently during the normal reload with no added delay.

The two animations genuinely cannot be blended into one. The reload clip drives its own operation through animation events, so forcing the animator into the catch state partway through leaves the reload unable to finish. Playing them in sequence is the honest version.

## 🏃 Skipping the catch when it matters

Saving the round is the wrong call when someone is pushing you, so you get two reload keys with two different trade offs:

| Key | What happens |
| --- | --- |
| **Reload** | Catch animation plays, the round ends up in your gear, the reload takes longer |
| **Quick reload** | No catch animation, full vanilla reload speed, the round is thrown on the floor |

**Quick reload is a separate keybind and Tarkov leaves it unbound by default.** Set it under *Settings > Controls* before expecting this to do anything. Many players bind it next to their reload key.

On weapons with a detachable magazine, Quick reload is Tarkov's own "drop the magazine to reload faster" action, and it is untouched by this mod. On a Mosin it normally does nothing different from a standard reload, so the mod gives it a purpose: it becomes your deliberate "I do not care about that round, load the gun now" key.

### Prefer it the other way round?

Turn on **Invert Reload Keys** and the two swap:

| Key | What happens with invert on |
| --- | --- |
| **Reload** | No catch animation, full vanilla reload speed, the round is thrown on the floor |
| **Quick reload** | Catch animation plays, the round ends up in your gear |

That keeps muscle memory intact: your normal reload behaves exactly like unmodded Tarkov, and saving the round becomes the deliberate thing you opt into. Just be aware that with nothing bound to Quick reload it leaves you no way to catch the round at all.

Set **Quick Reload Skips The Catch** to `false` if you would rather the catch always happen regardless of which key you use.

## 🔧 How it works

Everything happens in `Player.FirearmController.ReloadWithAmmo`, which is prefixed. When the weapon needs its bolt opened for an internal reload and there is a live round chambered, the round is dealt with before the reload is allowed to start, while it is still sitting in the chamber slot and still has a parent to be moved out of.

**With the animation**, the mod runs EFT's own chamber unload operation, handing it a real destination address in your gear. That address is what makes the operation raise the animator's `LoadOne` flag, which is the catch-and-stow variant rather than a plain eject. The round is committed from the operation's completion callback, the same point vanilla completes the inventory move from, and the reload is re-issued on the next frame.

**Without it**, the round is simply stowed there and then.

Either way the reload begins with an empty chamber, so it never has anything to throw.

A prefix on `WeaponManagerClass.ThrowPatronAsLoot` sits behind that as a safety net, matched to the one `source` tag the internal magazine reload uses. Almost nothing reaches it in practice, and every other throw the game makes passes straight through untouched.

No reload state machine is reimplemented anywhere, and if there is nowhere to put a round the mod stands down at every stage and lets it drop.

## 🤝 Fika

Suppressing the throw also suppresses the loot item that would have been created, so remote players do not end up looking at a cartridge that no longer exists. Even so, it is recommended that every client in the raid runs the mod so that all of them agree on what happened to the round.

Hosts can enforce this by adding the plugin GUID to `client.mods.required` in the Fika server config:

```jsonc
"required": [ "de.tosox.catchchamberedrounds" ]
```

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.
