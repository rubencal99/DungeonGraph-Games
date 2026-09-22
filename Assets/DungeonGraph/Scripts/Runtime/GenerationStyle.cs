namespace DungeonGraph
{
    /// <summary>
    /// Algorithm used to lay out rooms. Selected in the Dungeon Tools panel and,
    /// once implemented on the runtime side, on DungeonGenerator.
    /// Each style owns its own hyperparameters; the parameters in Basic Settings
    /// are shared by all styles.
    /// </summary>
    public enum GenerationStyle
    {
        /// <summary>
        /// Force-directed layout. Rooms are scattered inside a circle and pulled into place
        /// by spring attraction along connections and repulsion between all pairs.
        /// Produces loose, cave-like layouts. See OrganicGeneration.
        /// </summary>
        Organic = 0,

        /// <summary>
        /// Grid flood-fill layout: starting from the Start room, each connected room claims a
        /// free cell on an integer grid next to the room it branches from (classic Isaac/Gungeon
        /// room placement). Row/column spacing adapts to the largest room on that grid line, so
        /// Small/Medium/Large rooms can share the same grid without clipping. See
        /// <see cref="FloodFillGeneration"/>.
        /// </summary>
        /// <remarks>
        /// Displayed to users as "Grid" (the Dungeon Tools panel tab reads "Grid", not "Flood
        /// Fill") — the enum member and backing class kept their original internal name.
        /// </remarks>
        FloodFill = 2
    }
}
