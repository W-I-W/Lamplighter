using Godot;
using System;
using System.Linq;

namespace Lamplighter;

public partial class Game
{
    void BuildDupeScript()
    {
        S(.3f, () =>
        {
            Menu = Mn.None; noHeal = true; TestNoSpawn = true; Enemies.Clear();
            ReturnCraftIngredients(); CloseWindows(); Drops.Clear();
            Array.Clear(Inv); Inv[It.Wood] = 170; Inv[It.Stone] = 130; Inv[It.Copper] = 80;
            StockAdd("furnace", 25); int furnace = InventoryItemId("furnace");
            var tile = ToTile(PPos + FreeAdj()); SetObj(tile.X, tile.Y, Tl.StorageChest); OpenChest(tile.X, tile.Y);
            Array.Clear(ChestCells(tile));
            int[] items = { It.Wood, It.Stone, It.Copper, furnace };
            int[] start = items.Select(InventoryCount).ToArray(); var random = new Random(98471); string failure = null;
            for (int step = 0; step < 4000 && failure == null; step++)
            {
                var bag = InventorySlots(); int b = random.Next(bag.Count), q = random.Next(QuickSlotCount);
                int c = random.Next(ChestSlots), target = random.Next(ChestSlots), n = random.Next(1, 100);
                var stack = bag[b]; var chest = ChestStacks()[c]; int operation = random.Next(10);
                switch (operation)
                {
                    case 0: if (stack.n > 0) DepositChestSlot(b, false, stack.item, Math.Min(n, stack.n), c); break;
                    case 1: { var s = QuickStack(q); if (s.n > 0) DepositChestSlot(q, true, s.item, Math.Min(n, s.n), c); break; }
                    case 2: if (chest.n > 0) WithdrawChestToBag(c, b, chest.item, Math.Min(n, chest.n)); break;
                    case 3: if (chest.n > 0) MoveChestToQuick(c, q, Math.Min(n, chest.n), random.Next(2) == 0); break;
                    case 4: if (chest.n > 0) MoveChestStack(c, target, Math.Min(n, chest.n)); break;
                    case 5: if (stack.n > 0) MoveToQuickSlot(b, q, stack.item, Math.Min(n, stack.n)); break;
                    case 6: { var s = QuickStack(q); if (s.n > 0) MoveQuickToBag(q, b, Math.Min(n, s.n)); break; }
                    case 7: SwapBagQuick(b, q); break;
                    case 8: ChestMove(random.Next(2) == 0, random.Next(3) switch { 0 => It.Wood, 1 => It.Stone, _ => It.Copper }, n); break;
                    case 9: if (stack.n > 0) MoveInventoryStack(b, random.Next(bag.Count), stack.item, Math.Min(n, stack.n)); break;
                }
                bag = InventorySlots(); var chestAfter = ChestStacks();
                for (int itemIndex = 0; itemIndex < items.Length; itemIndex++)
                {
                    int item = items[itemIndex]; int owned = InventoryCount(item);
                    int inChest = chestAfter.Where(s => s.item == item).Sum(s => s.n);
                    int inBag = bag.Where(s => s.item == item).Sum(s => s.n);
                    if (owned + inChest != start[itemIndex] || inBag + QuickCount(item) != owned)
                    {
                        failure = $"step {step}, op {operation}, item {item}: inventory {owned}, bag {inBag}, quick {QuickCount(item)}, chest {inChest}, expected {start[itemIndex]}";
                        break;
                    }
                }
            }
            GD.Print(failure == null ? "[check] PASS dupe: 4000 inventory and chest transfers conserve every stack" : "[check] FAIL dupe: " + failure);
            int failures = failure == null ? 0 : 1;
            string oldRoot = SaveManager.Root;
            try
            {
                SaveManager.Root = System.IO.Path.Combine(testDir, "atomic-save");
                var character = new CharacterSave { Id = "audit-character", Inv = new int[It.Count] };
                var world = new WorldSave { Id = "audit-world", ChestCells = new[] { new[] { new ChestCell { Key = "res:0", Count = 0 } } } };
                character.Inv[It.Wood] = 10;
                bool initial = SaveManager.SaveSession(character, world);
                character.Inv[It.Wood] = 0; world.ChestCells[0][0].Count = 10;
                bool prepared = SaveManager.PrepareSessionSave(character, world);
                var recoveredWorld = SaveManager.LoadWorld(world.Id);
                var recoveredCharacter = SaveManager.LoadCharacter(character.Id);
                bool deposit = initial && prepared && recoveredWorld?.ChestCells[0][0].Count == 10 && recoveredCharacter?.Inv[It.Wood] == 0;
                GD.Print(deposit ? "[check] PASS dupe: interrupted chest deposit recovers both saves" : "[check] FAIL dupe: interrupted chest deposit was not recovered");
                if (!deposit) failures++;

                character.Inv[It.Wood] = 10; world.ChestCells[0][0].Count = 0;
                prepared = SaveManager.PrepareSessionSave(character, world);
                // Simulate a crash after the character file was replaced but before the world file.
                var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
                System.IO.File.WriteAllText(SaveManager.CharPath(character.Id), System.Text.Json.JsonSerializer.Serialize(character, options));
                recoveredCharacter = SaveManager.LoadCharacter(character.Id);
                recoveredWorld = SaveManager.LoadWorld(world.Id);
                bool withdrawal = prepared && recoveredCharacter?.Inv[It.Wood] == 10 && recoveredWorld?.ChestCells[0][0].Count == 0;
                GD.Print(withdrawal ? "[check] PASS dupe: interrupted chest withdrawal recovers both saves" : "[check] FAIL dupe: interrupted chest withdrawal was not recovered");
                if (!withdrawal) failures++;

                var blockedWorld = new WorldSave { Id = "blocked-world", Stock = new[] { 10 } };
                character.Inv[It.Wood] = 0;
                string blockedPath = SaveManager.WorldPath(blockedWorld.Id);
                System.IO.Directory.CreateDirectory(blockedPath);
                bool rejected = !SaveManager.SaveSession(character, blockedWorld) && SaveManager.LoadCharacter(character.Id) == null;
                System.IO.Directory.Delete(blockedPath);
                bool resumed = SaveManager.RecoverSessionSave() && SaveManager.LoadWorld(blockedWorld.Id)?.Stock[0] == 10 &&
                    SaveManager.LoadCharacter(character.Id)?.Inv[It.Wood] == 0;
                bool blocked = rejected && resumed;
                GD.Print(blocked ? "[check] PASS dupe: failed world write blocks inconsistent loads and can recover" : "[check] FAIL dupe: failed world write did not recover safely");
                if (!blocked) failures++;
            }
            finally { SaveManager.Root = oldRoot; }
            GD.Print("[check] SUMMARY failures=" + failures);
        });
    }
}
