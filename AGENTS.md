# Monitoring UI conventions

- Put seasonal or event-only enemy types in the **Events** section below the boss cards in the left column. Infected (`infectedScreamer`) is the first example. Do not add event mobs to the right AI PMC / Scav / Rogue / Raider column.
- Register new event cards through `MainWindow.RenderEventMobs` and reuse `RenderEventMobCard`. Keep map/event eligibility in `RenderEventMobs`, and each enemy type's tracking state separate in `RaidState`. Include new state in death matching, raid-end handling, and the render signature as needed.
- Halloween styling is opt-in for the `infectedScreamer` card only. Keep other event cards (including infected Tagilla), the Events heading, and the shared app theme unchanged when extending this decoration.
- `infectedTagilla` belongs to Events, separately from ordinary `bossTagilla`. Show its card when a spawn decision or activation is logged. A probability alone does not confirm activation: keep Planned / NotPlanned / Confirmed distinct, and include actual activations in death matching and raid-end handling.
- Localize user-visible text for Korean, English and Simplified Chinese through `I18n.T` and the English dictionary in `Localization.cs`.
- Keep the player's kill count prominent in the upper-right raid header. Raid IDs are internal correlation data; do not show them in the monitoring UI.
- PMC cards must show Planned / NotPlanned / Unknown before any activation, then show life counts once activated. Omit the redundant "Initial spawn" row, not the pending spawn decision itself.
- Run `./test.ps1` after changing tracking or rendering. Use `./test.ps1 -IncludePrivateLogs` when the ignored archive in `work/test-logs` is available. Keep original logs private; commit only anonymized fixtures under `tests/fixtures`.
