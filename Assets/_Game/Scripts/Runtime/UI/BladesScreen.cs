using System.Collections.Generic;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>The blades panel: choose the cut effect; locked blades show the achievement that opens them.</summary>
    public sealed class BladesScreen : UiScreen
    {
        [SerializeField] CollectionManager collection;
        [SerializeField] UiRoot root;
        [Tooltip("Inactive template, cloned once per blade.")]
        [SerializeField] BladeTileView tileTemplate;
        [Tooltip("Rows the tiles are laid out in, top to bottom; the last row takes whatever is left.")]
        [SerializeField] RectTransform[] rows = new RectTransform[0];
        [SerializeField, Min(1)] int perRow = 4;
        [SerializeField] UnityEngine.UI.Button backButton;

        readonly List<BladeTileView> tiles = new();

        protected override void Awake()
        {
            base.Awake();
            backButton.onClick.AddListener(() => root.ShowPage(MenuPage.Home));
            tileTemplate.gameObject.SetActive(false);
            foreach (BladeDefinition blade in collection.Database.blades)
            {
                if (blade == null) continue;
                BladeTileView tile = Instantiate(tileTemplate, rows[Mathf.Min(tiles.Count / perRow, rows.Length - 1)]);
                tile.gameObject.SetActive(true);
                tile.Button.onClick.AddListener(() => collection.Select(blade));
                tiles.Add(tile);
            }
        }

        void OnEnable()
        {
            collection.Changed += Refresh;
            Loc.Changed += Refresh;
        }

        void OnDisable()
        {
            collection.Changed -= Refresh;
            Loc.Changed -= Refresh;
        }

        protected override void OnShow() => Refresh();

        void Refresh()
        {
            CollectionProgress progress = collection.Progress;
            BladeDefinition current = collection.CurrentBlade;
            int i = 0;
            foreach (BladeDefinition blade in collection.Database.blades)
            {
                if (blade == null) continue;
                UnlockRequirement unlock = blade.unlock;
                tiles[i++].Bind(blade, collection.IsUnlocked(blade), blade == current, UiText.Requirement(unlock, progress.CardCount),
                    progress.Current(unlock), unlock.kind == UnlockKind.Free ? 0 : unlock.amount);
            }
        }
    }
}
