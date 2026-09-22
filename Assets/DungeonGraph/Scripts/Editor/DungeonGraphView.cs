using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System;
using System.Linq;
using CorridorType = DungeonGraph.CorridorType;

namespace DungeonGraph.Editor
{
    public class DungeonGraphView : GraphView
    {
        private DungeonGraphAsset m_dungeonGraph;
        private DungeonGraphAsset m_lastProcessedGraph;
        private SerializedObject m_serializedObject;
        private DungeonGraphEditorWindow m_window;
        public DungeonGraphEditorWindow window => m_window;

        public List<DungeonGraphEditorNode> m_graphNodes;
        public Dictionary<string, DungeonGraphEditorNode> m_nodeDictionary;
        public Dictionary<Edge, DungeonGraphConnection> m_connectionDictionary;

        private DungeonGraphWindowSearchProvider m_searchProvider;

        private Blackboard m_toolsBoard;
        private Blackboard m_nodeSettingsBoard;
        private DungeonGraphEditorNode m_selectedNode;
        private bool m_toolsBoardPositioned = false;

        // Selected generation algorithm - drives which parameters the panel shows
        private GenerationStyle m_generationStyle = GenerationStyle.Organic;

        // Organic-only generation parameters
        private float m_areaPlacementFactor = 2.0f;
        private float m_repulsionFactor = 1.0f;
        private int m_simulationIterations = 100;
        private float m_stiffnessFactor = 1.0f;
        private RepulsionScalingMode m_repulsionScalingMode = RepulsionScalingMode.Default;
        private float m_bendFactor = 0.0f;

        // Grid-only generation parameters (backing class/enum member: FloodFill — see GenerationStyle)
        private int m_floodFillMaxCorridorLength = 40;
        private int m_floodFillMaxBacktrackAttempts = 6;
        private int m_floodFillSeed = 0;

        // Parameters shared by every generation style
        private bool m_forceMode = false;
        private float m_chaosFactor = 0.0f;
        private bool m_realTimeSimulation = false;
        private float m_simulationSpeed = 10f;
        private float m_idealDistance = 20f;
        private bool m_allowRoomOverlap = false;
        private int m_maxRoomRegenerations = 3;
        private int m_maxCorridorRegenerations = 3;

        // UI toggle for the Advanced Organic Settings foldout (the shared Advanced Settings
        // section is a plain, always-visible BlackboardSection — no collapse state needed).
        private bool m_showStyleAdvanced = false;

        // Rebuilt whenever the generation style changes
        private VisualElement m_styleParametersContainer;

        // Held because Force Mode (Basic Settings) disables Simulation Iterations, which
        // lives in the Organic style section and is destroyed on every style switch.
        private IntegerField m_simulationIterationsField;

        // Held so a style switch can re-evaluate whether Allow Room Overlap should gray this
        // out — Grid repurposes Max Room Regenerations for corridor-length retries, where
        // Allow Room Overlap (an Organic-only concept) has no bearing. Also held, along with the
        // three fields below, so RefreshStyleTuningControls can push a newly selected style's
        // per-graph tuning into these controls without rebuilding them.
        private IntegerField m_maxRoomRegenerationsField;
        private Slider m_idealDistanceSlider;
        private Slider m_chaosSlider;
        private Toggle m_forceModeToggle;

        private Button m_generateDungeonBtn;
        private Button m_generateRoomsBtn;
        private Button m_generateCorridorsBtn;

        // One tab button per generation style, keyed by style, so clicking a tab can flip its
        // own visuals and the previously-active tab's without rebuilding the whole row.
        private readonly Dictionary<GenerationStyle, Button> m_styleTabButtons = new Dictionary<GenerationStyle, Button>();

        // Corridor generation parameters
        private UnityEngine.Tilemaps.TileBase m_corridorTile = null;
        private int m_corridorWidth = 2;
        private CorridorType m_corridorType = CorridorType.Direct;

        // Floor selection
        private List<DungeonFloorConfig> m_availableFloors = new List<DungeonFloorConfig>();
        private int m_selectedFloorIndex = 0;
        private string m_currentFloorPath = "";

        // EditorPrefs keys for persistence
        private const string PREF_GENERATION_STYLE = "DungeonGraph.GenerationStyle";
        private const string PREF_SHOW_STYLE_ADVANCED = "DungeonGraph.ShowStyleAdvanced";
        private const string PREF_FLOODFILL_MAX_CORRIDOR_LENGTH = "DungeonGraph.FloodFillMaxCorridorLength";
        private const string PREF_FLOODFILL_MAX_BACKTRACK_ATTEMPTS = "DungeonGraph.FloodFillMaxBacktrackAttempts";
        private const string PREF_FLOODFILL_SEED = "DungeonGraph.FloodFillSeed";
        private const string PREF_AREA_PLACEMENT = "DungeonGraph.AreaPlacement";
        private const string PREF_REPULSION = "DungeonGraph.Repulsion";
        private const string PREF_ITERATIONS = "DungeonGraph.Iterations";
        private const string PREF_STIFFNESS = "DungeonGraph.Stiffness";
        private const string PREF_REPULSION_SCALING = "DungeonGraph.RepulsionScaling";
        private const string PREF_BEND_FACTOR = "DungeonGraph.BendFactor";
        private const string PREF_REALTIME_SIMULATION = "DungeonGraph.RealTimeSimulation";
        private const string PREF_SIMULATION_SPEED = "DungeonGraph.SimulationSpeed";
        private const string PREF_ALLOW_ROOM_OVERLAP = "DungeonGraph.AllowRoomOverlap";
        private const string PREF_MAX_CORRIDOR_REGENERATIONS = "DungeonGraph.MaxCorridorRegenerations";
        private const string PREF_CORRIDOR_TILE = "DungeonGraph.CorridorTile";
        private const string PREF_CORRIDOR_WIDTH = "DungeonGraph.CorridorWidth";
        private const string PREF_CORRIDOR_TYPE = "DungeonGraph.CorridorType";
        private const string PREF_SELECTED_FLOOR = "DungeonGraph.SelectedFloor";

        public DungeonGraphView(SerializedObject serializedObject, DungeonGraphEditorWindow window)
        {
            m_serializedObject = serializedObject;
            m_dungeonGraph = (DungeonGraphAsset)serializedObject.targetObject;
            m_window = window;

            m_graphNodes = new List<DungeonGraphEditorNode>();
            m_nodeDictionary = new Dictionary<string, DungeonGraphEditorNode>();
            m_connectionDictionary = new Dictionary<Edge, DungeonGraphConnection>();

            // Load persisted parameters
            LoadPreferences();

            m_searchProvider = ScriptableObject.CreateInstance<DungeonGraphWindowSearchProvider>();
            m_searchProvider.graph = this;
            this.nodeCreationRequest = ShowSearchWindow;

            StyleSheet style = DungeonGraphPaths.LoadEditorStyleSheet();
            if (style != null)
                styleSheets.Add(style);

            GridBackground background = new GridBackground();
            background.name = "Grid";
            Add(background);
            //background.SendToBack();

            // Ensure the grid fills the view and cannot eat mouse events
            background.StretchToParentSize();
            background.pickingMode = PickingMode.Ignore;

            // Make sure it really sits behind everything else
            background.SendToBack();
            //background.style.zIndex = 0;

            // Keep the graph content above the grid (and pickable)
            this.style.flexGrow = 1;
            //contentViewContainer.style.zIndex = 1;
            contentViewContainer.pickingMode = PickingMode.Position;

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ClickSelector());
            this.SetupZoom(0.5f / 3f, 10f);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            // Enable copy/paste
            serializeGraphElements = SerializeGraphElementsCallback;
            unserializeAndPaste = UnserializeAndPasteCallback;
            canPasteSerializedData = CanPasteSerializedDataCallback;

            DrawNodes();
            DrawConnections();

            RegisterCallback<MouseDownEvent>(OnGlobalMouseDown, TrickleDown.TrickleDown);
            RegisterCallback<MouseUpEvent>(OnGlobalMouseUp, TrickleDown.TrickleDown);

            // Set the default pickability once after building the view
            SetPickabilityDuringDrag(false, null, Direction.Output);

            // var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/DungeonGraph/Scripts/Editor/DungeonGraphEditor.uss");
            // if (sheet != null) styleSheets.Add(sheet);

            graphViewChanged += OnGraphViewChangedEvent;

            // Track node selection changes
            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);

            BuildToolsPanel();
            BuildNodeSettingsPanel();

            // Sync dropdown when the user creates/deletes/renames floor folders in the Project window
            DungeonFloorsWatcher.OnFloorsChanged += OnExternalFloorsChanged;
            RegisterCallback<DetachFromPanelEvent>(_ => DungeonFloorsWatcher.OnFloorsChanged -= OnExternalFloorsChanged);
        }

        private void OnExternalFloorsChanged()
        {
            RefreshFloorList(null);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            // Use schedule to defer the check until after selection is updated
            schedule.Execute(() =>
            {
                // Check current selection
                var selectedNodes = selection.OfType<DungeonGraphEditorNode>().ToList();

                if (selectedNodes.Count == 1)
                {
                    // Single node selected
                    var node = selectedNodes[0];
                    UpdateNodeSettingsPanel(node);
                }
                else if (selectedNodes.Count == 0)
                {
                    // Nothing selected or non-node element selected
                    UpdateNodeSettingsPanel(null);
                }
                // For multiple nodes selected, keep the current panel state
            });
        }

        /// <summary>
        /// Current display resolution in UI Toolkit points (not pixels). Used as the basis for
        /// panel sizing instead of the DungeonGraphView's own layout size: a maximized editor
        /// window on a small laptop screen and a floated, un-maximized window on a large monitor
        /// can report similar view sizes, but should get very different default panel sizes.
        /// </summary>
        /// <remarks>
        /// Editor has no simple cross-platform API for "which monitor is this window on", so this
        /// uses the primary display's resolution as a reasonable proxy.
        /// </remarks>
        private static Vector2 GetScreenReferenceSize()
        {
            float pixelsPerPoint = Mathf.Max(EditorGUIUtility.pixelsPerPoint, 0.01f);
            Resolution res = Screen.currentResolution;
            return new Vector2(res.width / pixelsPerPoint, res.height / pixelsPerPoint);
        }

        /// <summary>A fraction of a screen dimension, clamped to a sane absolute range.</summary>
        private static float ScaledDimension(float screenDimension, float fraction, float min, float max)
            => Mathf.Clamp(screenDimension * fraction, min, max);

        private void ApplyStaticHeaderStyles(Blackboard board)
        {
            // Prevent the header row from shrinking when the panel is resized
            var header = board.Q("header");
            if (header != null)
            {
                header.style.flexShrink = 0;
                header.style.overflow = Overflow.Hidden;
            }

            // Keep title/subtitle on a single line; truncate with "…" rather than wrapping
            var titleLabel = board.Q<Label>("titleLabel");
            if (titleLabel != null)
            {
                titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
                titleLabel.style.overflow = Overflow.Hidden;
                titleLabel.style.textOverflow = TextOverflow.Ellipsis;
                titleLabel.style.flexShrink = 1;
            }

            var subTitleLabel = board.Q<Label>("subTitleLabel");
            if (subTitleLabel != null)
            {
                subTitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
                subTitleLabel.style.overflow = Overflow.Hidden;
                subTitleLabel.style.textOverflow = TextOverflow.Ellipsis;
                subTitleLabel.style.flexShrink = 1;
            }
        }

        private void BuildToolsPanel()
        {
            // Preserve the panel's current rect across a rebuild (floor list refresh, style
            // change) so an unrelated UI refresh doesn't undo a manual resize/move.
            Rect? previousRect = m_toolsBoard != null ? m_toolsBoard.GetPosition() : (Rect?)null;

            // Callers that rebuild the panel (floor list refresh, style change) would otherwise
            // leave the previous, now-empty Blackboard parented to the view forever.
            if (m_toolsBoard != null && m_toolsBoard.parent != null)
                m_toolsBoard.RemoveFromHierarchy();

            // A movable ShaderGraph-style panel
            m_toolsBoard = new Blackboard(this)
            {
                title = "Dungeon Tools",
                subTitle = m_dungeonGraph != null ? m_dungeonGraph.name : string.Empty
            };

            // Make it draggable | collapsible | resizable
            m_toolsBoard.capabilities |= Capabilities.Movable | Capabilities.Collapsible | Capabilities.Resizable;

            // Size bounds scale with the physical screen so the panel is never a fixed pixel
            // size that dwarfs the graph view on a small monitor, while still leaving room to
            // drag-resize between a sensible min and max.
            Vector2 screen = GetScreenReferenceSize();
            float minWidth     = ScaledDimension(screen.x, 0.14f, 240f, 320f);
            float maxWidth     = ScaledDimension(screen.x, 0.40f, 420f, 760f);
            float defaultWidth = ScaledDimension(screen.x, 0.20f, minWidth, maxWidth);
            float minHeight     = ScaledDimension(screen.y, 0.24f, 220f, 340f);
            float maxHeight     = ScaledDimension(screen.y, 0.85f, 500f, 1200f);
            float defaultHeight = ScaledDimension(screen.y, 0.55f, minHeight, maxHeight);

            float width, height, xPos, yPos;
            if (previousRect.HasValue && previousRect.Value.width > 0 && previousRect.Value.height > 0)
            {
                width  = Mathf.Clamp(previousRect.Value.width, minWidth, maxWidth);
                height = Mathf.Clamp(previousRect.Value.height, minHeight, maxHeight);
                xPos   = previousRect.Value.x;
                yPos   = previousRect.Value.y;
            }
            else
            {
                width  = defaultWidth;
                height = defaultHeight;
                // Use a safe default position if layout width is not yet initialized
                xPos = layout.width > width ? layout.width - width - 16 : 16;
                yPos = 16;
            }

            // Position at top-right corner with reasonable default size
            // Will be repositioned on geometry change to stick to top-right
            m_toolsBoard.SetPosition(new Rect(xPos, yPos, width, height));

            m_toolsBoard.style.minWidth  = minWidth;
            m_toolsBoard.style.minHeight = minHeight;
            m_toolsBoard.style.maxWidth  = maxWidth;
            m_toolsBoard.style.maxHeight = maxHeight;

            var addButton = m_toolsBoard.Q<Button>("addButton");
            if (addButton != null)
                addButton.style.display = DisplayStyle.None;

            ApplyStaticHeaderStyles(m_toolsBoard);

            // All content lives inside this scroll view so the panel can be freely shrunk
            var mainScrollView = new ScrollView(ScrollViewMode.Vertical);
            mainScrollView.style.flexGrow = 1;
            mainScrollView.style.flexShrink = 1;
            mainScrollView.style.minHeight = 0;
            mainScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            mainScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            // ===== ACTIONS =====
            var actionsHeader = new Label("Actions");
            actionsHeader.AddToClassList("section-header");
            mainScrollView.Add(actionsHeader);

            var actions = new VisualElement();
            actions.style.marginBottom = 10;

            m_generateDungeonBtn = new Button(GenerateDungeon)
            {
                text = "Generate Dungeon",
                tooltip = "Generate complete dungeon with rooms and corridors"
            };
            m_generateDungeonBtn.style.height = 35;
            m_generateDungeonBtn.style.marginBottom = 6;
            actions.Add(m_generateDungeonBtn);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.SpaceBetween;

            m_generateRoomsBtn = new Button(() => GenerateRooms())
            {
                text = "Generate Rooms",
                tooltip = "Generate only rooms using physics simulation"
            };
            m_generateRoomsBtn.style.height = 28;
            m_generateRoomsBtn.style.flexGrow = 1;
            m_generateRoomsBtn.style.marginRight = 4;
            buttonRow.Add(m_generateRoomsBtn);

            m_generateCorridorsBtn = new Button(GenerateCorridors)
            {
                text = "Generate Corridors",
                tooltip = "Generate corridors connecting existing rooms"
            };
            m_generateCorridorsBtn.style.height = 28;
            m_generateCorridorsBtn.style.flexGrow = 1;
            buttonRow.Add(m_generateCorridorsBtn);

            actions.Add(buttonRow);

            var clearBtn = new Button(ClearDungeon)
            {
                text = "Clear Dungeon",
                tooltip = "Remove all generated rooms and corridors"
            };
            clearBtn.style.height = 28;
            clearBtn.style.marginTop = 6;
            actions.Add(clearBtn);

            mainScrollView.Add(actions);

            // ===== FLOOR SELECTION =====
            var floorHeader = new Label("Dungeon Floor");
            floorHeader.AddToClassList("section-header");
            floorHeader.style.marginTop = 10;
            mainScrollView.Add(floorHeader);

            var floorContainer = new VisualElement();
            floorContainer.style.marginBottom = 10;

            var floorDropdown = new UnityEngine.UIElements.PopupField<DungeonFloorConfig>(
                "Selected Floor",
                m_availableFloors,
                m_selectedFloorIndex >= 0 && m_selectedFloorIndex < m_availableFloors.Count ? m_availableFloors[m_selectedFloorIndex] : null
            );
            floorDropdown.tooltip = "Select which dungeon floor to use for room generation";
            floorDropdown.RegisterValueChangedCallback(evt =>
            {
                m_selectedFloorIndex = m_availableFloors.IndexOf(evt.newValue);
                UpdateCurrentFloorPath();
                SavePreferences();
            });
            floorContainer.Add(floorDropdown);

            // --- Create buttons row ---
            var floorCreateRow = new VisualElement();
            floorCreateRow.style.flexDirection = FlexDirection.Row;
            floorCreateRow.style.marginTop = 4;

            var createFloorBtn = new Button(() =>
            {
                CreateFloorWindow.ShowWindow((floorName) =>
                {
                    if (!DungeonFloorManager.CreateNewFloor(floorName))
                        return false; // DungeonFloorManager already showed the specific error dialog

                    RefreshFloorList(floorName);
                    EditorUtility.DisplayDialog("Success",
                        $"Floor '{floorName}' created successfully with all standard and custom node folders!", "OK");
                    return true;
                });
            })
            {
                text = "Create New Floor",
                tooltip = "Create a floor with all standard node type subfolders and blank room prefabs"
            };
            createFloorBtn.style.height = 24;
            createFloorBtn.style.flexGrow = 1;
            createFloorBtn.style.marginRight = 3;
            floorCreateRow.Add(createFloorBtn);

            var createEmptyBtn = new Button(() =>
            {
                CreateFloorWindow.ShowWindow(
                    (folderName) =>
                    {
                        if (!DungeonFloorManager.CreateEmptyFloor(folderName))
                        {
                            EditorUtility.DisplayDialog("Error",
                                $"Failed to create folder '{folderName}'. It may already exist.", "OK");
                        }
                        else
                        {
                            RefreshFloorList(folderName);
                        }
                        return true; // always close — empty folders don't need Addressable group validation
                    },
                    windowTitle: "Create Empty Folder",
                    helpText: "Creates a bare folder in the floors directory with no subfolders or room prefabs."
                );
            })
            {
                text = "Create Empty Folder",
                tooltip = "Create a bare folder in the floors directory — no subfolders or prefabs generated"
            };
            createEmptyBtn.style.height = 24;
            createEmptyBtn.style.flexGrow = 1;
            floorCreateRow.Add(createEmptyBtn);

            floorContainer.Add(floorCreateRow);

            mainScrollView.Add(floorContainer);

            // ===== GENERATION STYLE SETTINGS =====
            var generationStyleHeader = new Label("Generation Style Settings");
            generationStyleHeader.AddToClassList("section-header");
            generationStyleHeader.style.marginTop = 10;
            mainScrollView.Add(generationStyleHeader);

            mainScrollView.Add(BuildGenerationStyleTabs());

            // Shares the selected tab's dark shade, so the tab and the settings unique to it
            // read as one connected block. Basic/Advanced Settings below use the panel's default
            // background instead — only this section is tied to which tab is selected.
            m_styleParametersContainer = new VisualElement();
            m_styleParametersContainer.name = "style-parameters";
            m_styleParametersContainer.style.backgroundColor = kSelectedTabOverlay;
            mainScrollView.Add(m_styleParametersContainer);
            RebuildStyleParameters();

            // ===== BASIC SETTINGS =====
            var basicSettingsHeader = new Label("Basic Settings");
            basicSettingsHeader.AddToClassList("section-header");
            basicSettingsHeader.style.marginTop = 10;
            mainScrollView.Add(basicSettingsHeader);

            // ===== ROOM SETTINGS =====
            var roomSettings = new BlackboardSection { title = "Room Settings" };
            roomSettings.name = "room-settings";

            var idealDistanceSlider = new Slider("Ideal Distance", 1f, 50f) { value = m_idealDistance };
            idealDistanceSlider.showInputField = true;
            idealDistanceSlider.tooltip = "Target distance between connected rooms. Remembered per " +
                                          "generation style, per graph — Organic and Grid each keep " +
                                          "their own value for this graph.";
            idealDistanceSlider.RegisterValueChangedCallback(evt =>
            {
                m_idealDistance = evt.newValue;
                SavePreferences();
            });
            roomSettings.Add(idealDistanceSlider);
            m_idealDistanceSlider = idealDistanceSlider;

            mainScrollView.Add(roomSettings);

            // ===== CORRIDOR SETTINGS =====
            var corridorSettings = new BlackboardSection { title = "Corridor Settings" };
            corridorSettings.name = "corridor-settings";

            var corridorTileField = new ObjectField("Corridor Tile")
            {
                objectType = typeof(UnityEngine.Tilemaps.TileBase),
                value = m_corridorTile,
                tooltip = "Tile used to paint corridors connecting rooms"
            };
            corridorTileField.RegisterValueChangedCallback(evt =>
            {
                m_corridorTile = evt.newValue as UnityEngine.Tilemaps.TileBase;
                SavePreferences();
            });
            corridorSettings.Add(corridorTileField);

            var corridorWidthField = new IntegerField("Corridor Width") { value = m_corridorWidth };
            corridorWidthField.tooltip = "Width of corridors in tiles";
            corridorWidthField.RegisterValueChangedCallback(evt =>
            {
                m_corridorWidth = evt.newValue;
                SavePreferences();
            });
            corridorSettings.Add(corridorWidthField);

            var corridorTypeField = new EnumField("Corridor Type", m_corridorType);
            corridorTypeField.tooltip = "Pathfinding algorithm for corridor generation";
            corridorTypeField.RegisterValueChangedCallback(evt =>
            {
                m_corridorType = (CorridorType)evt.newValue;
                SavePreferences();
            });
            corridorSettings.Add(corridorTypeField);

            mainScrollView.Add(corridorSettings);

            // ===== ADVANCED SETTINGS (shared by all generation styles) =====
            // Same plain, always-visible section format as Room/Corridor Settings above — not a
            // collapsible foldout. (Advanced Organic Settings, inside the Organic tab's own
            // parameter section, is a separate foldout and keeps that collapsible behavior.)
            var advancedSettings = new BlackboardSection { title = "Advanced Settings" };
            advancedSettings.name = "advanced-settings";

            var forceModeToggle = new Toggle("Force Mode") { value = m_forceMode };
            forceModeToggle.tooltip = "Keep going until the layout resolves instead of stopping at the " +
                                      "configured attempt limits (Organic caps at 2096 iterations, Grid at " +
                                      "64 layout attempts). Remembered per generation style, per graph.";
            forceModeToggle.RegisterValueChangedCallback(evt =>
            {
                m_forceMode = evt.newValue;
                SavePreferences();
                // Only present while the Organic style is selected
                m_simulationIterationsField?.SetEnabled(!evt.newValue);
            });
            advancedSettings.Add(forceModeToggle);
            m_forceModeToggle = forceModeToggle;

            var chaosSlider = new Slider("Chaos Factor", 0.0f, 1.0f) { value = m_chaosFactor };
            chaosSlider.showInputField = true;
            chaosSlider.tooltip = "Randomness introduced during generation — room velocities under Organic, " +
                                  "grid-direction order under Grid (0 = ordered, 1 = chaotic). Remembered " +
                                  "per generation style, per graph.";
            chaosSlider.RegisterValueChangedCallback(evt =>
            {
                m_chaosFactor = evt.newValue;
                SavePreferences();
            });
            advancedSettings.Add(chaosSlider);
            m_chaosSlider = chaosSlider;

            var maxRoomRegenerationsField = new IntegerField("Max Room Regenerations") { value = m_maxRoomRegenerations };
            maxRoomRegenerationsField.tooltip = "Organic: attempts to regenerate the layout if room overlaps " +
                                                "are detected. Grid: attempts to re-roll placement if a " +
                                                "connection needs a corridor longer than its Max Corridor " +
                                                "Length. Remembered per generation style, per graph.";
            maxRoomRegenerationsField.SetEnabled(!m_allowRoomOverlap || m_generationStyle == GenerationStyle.FloodFill);
            maxRoomRegenerationsField.RegisterValueChangedCallback(evt =>
            {
                m_maxRoomRegenerations = evt.newValue;
                SavePreferences();
            });
            m_maxRoomRegenerationsField = maxRoomRegenerationsField;

            advancedSettings.Add(maxRoomRegenerationsField);

            var maxCorridorRegenerationsField = new IntegerField("Max Corridor Regenerations") { value = m_maxCorridorRegenerations };
            maxCorridorRegenerationsField.tooltip = "Maximum attempts to regenerate corridors if overlaps are detected";
            maxCorridorRegenerationsField.RegisterValueChangedCallback(evt =>
            {
                m_maxCorridorRegenerations = evt.newValue;
                SavePreferences();
            });
            advancedSettings.Add(maxCorridorRegenerationsField);

            mainScrollView.Add(advancedSettings);

            m_toolsBoard.Add(mainScrollView);
            Add(m_toolsBoard);
        }

        /// <summary>
        /// Darkens whatever background sits behind the selected style tab and its unique
        /// parameter section (<see cref="m_styleParametersContainer"/>) — the two visually read
        /// as one connected block. Basic/Advanced Settings stay at the panel's default color.
        /// </summary>
        private static readonly Color kSelectedTabOverlay = new Color(0f, 0f, 0f, 0.35f);

        /// <summary>Lightens whatever background sits behind an unselected style tab.</summary>
        private static readonly Color kUnselectedTabOverlay = new Color(1f, 1f, 1f, 0.16f);

        /// <summary>
        /// Builds the row of clickable "browser tab"-style buttons used to pick the generation
        /// style, replacing the previous dropdown. Each tab's hover tooltip carries the style's
        /// description instead of showing it in the panel body, keeping the tab row itself compact.
        /// </summary>
        private VisualElement BuildGenerationStyleTabs()
        {
            m_styleTabButtons.Clear();

            var tabRow = new VisualElement();
            tabRow.style.flexDirection = FlexDirection.Row;
            tabRow.style.marginBottom = 6;

            AddStyleTab(tabRow, GenerationStyle.Organic, "Organic",
                "Force-directed layout. Rooms are scattered inside a circle, then a physics " +
                "simulation pulls connected rooms together and pushes all rooms apart until the " +
                "layout settles. Produces loose, cave-like layouts.");

            AddStyleTab(tabRow, GenerationStyle.FloodFill, "Grid",
                "Grid flood-fill layout. Starting from the Start room, each connected room claims " +
                "a free cell on a grid next to the room it branches from. Row/column spacing " +
                "adapts to each room's size. Produces compact, grid-aligned layouts with short " +
                "corridors.");

            RefreshStyleTabVisuals();
            return tabRow;
        }

        private void AddStyleTab(VisualElement tabRow, GenerationStyle style, string label, string description)
        {
            var tab = new Button(() =>
            {
                // Order matters: switch the style first, then reload tuning for the *new* style,
                // then persist. Saving before the switch (or before the reload) would write the
                // outgoing style's live field values into the incoming style's tuning slot —
                // each field's own edit callback already saves the moment it changes, so the
                // outgoing style's tuning is already up to date on disk by the time we get here.
                m_generationStyle = style;
                RebuildStyleParameters();
                RefreshStyleTuningControls();
                SavePreferences();
            })
            {
                text = label,
                tooltip = description
            };

            tab.style.flexGrow = 1;
            tab.style.marginTop = 0;
            tab.style.marginBottom = 0;
            tab.style.marginLeft = 0;
            tab.style.marginRight = style == GenerationStyle.Organic ? 1 : 0;
            tab.style.borderBottomLeftRadius = 0;
            tab.style.borderBottomRightRadius = 0;

            tabRow.Add(tab);
            m_styleTabButtons[style] = tab;
        }

        /// <summary>
        /// Darkens every inactive tab relative to the panel and clears that override on the
        /// active one, so the selected tab reads as "part of the panel below it" the way a
        /// browser's active tab does, without needing to know the panel's exact theme color.
        /// </summary>
        private void RefreshStyleTabVisuals()
        {
            foreach (var kvp in m_styleTabButtons)
            {
                bool active = kvp.Key == m_generationStyle;
                kvp.Value.style.backgroundColor = active ? new StyleColor(kSelectedTabOverlay) : new StyleColor(kUnselectedTabOverlay);
                kvp.Value.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        /// <summary>
        /// Rebuilds the block under the Generation Style tabs so only the selected style's
        /// hyperparameters are ever shown. Called on construction and on every style change.
        /// </summary>
        private void RebuildStyleParameters()
        {
            if (m_styleParametersContainer == null) return;

            m_styleParametersContainer.Clear();
            m_simulationIterationsField = null; // owned by the Organic block that was just discarded

            switch (m_generationStyle)
            {
                case GenerationStyle.FloodFill:
                    m_styleParametersContainer.Add(BuildFloodFillParameters());
                    break;

                case GenerationStyle.Organic:
                default:
                    m_styleParametersContainer.Add(BuildOrganicParameters());
                    break;
            }

            RefreshStyleTabVisuals();
        }

        /// <summary>
        /// Hyperparameters unique to the force-directed Organic layout. Real-Time Simulation,
        /// Simulation Speed, and Allow Room Overlap live here rather than in the shared Basic
        /// Settings block because they are Organic-only concepts: Grid always resolves instantly
        /// (no animated placement) and never overlaps rooms by construction, so surfacing those
        /// controls while Grid is selected would just be noise.
        /// </summary>
        private VisualElement BuildOrganicParameters()
        {
            var section = new BlackboardSection { title = "Organic Parameters" };
            section.name = "organic-parameters";

            var realTimeToggle = new Toggle("Real-Time Simulation") { value = m_realTimeSimulation };
            realTimeToggle.tooltip = "Animate generation in real-time instead of resolving the whole layout at once";
            section.Add(realTimeToggle);

            var speedSlider = new Slider("Simulation Speed", 1f, 1000f) { value = m_simulationSpeed };
            speedSlider.showInputField = true;
            speedSlider.tooltip = "Steps per second while real-time generation is enabled";
            speedSlider.SetEnabled(m_realTimeSimulation);
            speedSlider.RegisterValueChangedCallback(evt =>
            {
                m_simulationSpeed = evt.newValue;
                SavePreferences();
            });

            realTimeToggle.RegisterValueChangedCallback(evt =>
            {
                m_realTimeSimulation = evt.newValue;
                SavePreferences();
                speedSlider.SetEnabled(evt.newValue);
            });
            section.Add(speedSlider);

            var repulsionField = new Slider("Repulsion Factor", 0.1f, 40f) { value = m_repulsionFactor };
            repulsionField.showInputField = true;
            repulsionField.tooltip = "Strength of repulsion between rooms to prevent overlap";
            repulsionField.RegisterValueChangedCallback(evt =>
            {
                m_repulsionFactor = evt.newValue;
                SavePreferences();
            });
            section.Add(repulsionField);

            var stiffnessSlider = new Slider("Stiffness Factor", 0.1f, 20.0f) { value = m_stiffnessFactor };
            stiffnessSlider.showInputField = true;
            stiffnessSlider.tooltip = "How strongly rooms are pulled toward their ideal distance";
            stiffnessSlider.RegisterValueChangedCallback(evt =>
            {
                m_stiffnessFactor = evt.newValue;
                SavePreferences();
            });
            section.Add(stiffnessSlider);

            var iterationsField = new IntegerField("Simulation Iterations") { value = m_simulationIterations };
            iterationsField.tooltip = "Number of physics iterations to run (higher = more stable layout)";
            iterationsField.SetEnabled(!m_forceMode);
            iterationsField.RegisterValueChangedCallback(evt =>
            {
                m_simulationIterations = evt.newValue;
                SavePreferences();
            });
            section.Add(iterationsField);
            m_simulationIterationsField = iterationsField; // Force Mode in Basic Settings toggles this

            // ----- Advanced -----
            var advancedFoldout = new Foldout { text = "Advanced Organic Settings", value = m_showStyleAdvanced };
            advancedFoldout.tooltip = "Fine-tuning parameters for the force-directed layout";

            var advancedContent = new VisualElement();

            var areaField = new FloatField("Area Placement Factor") { value = m_areaPlacementFactor };
            areaField.tooltip = "Multiplier for initial room placement spread";
            areaField.RegisterValueChangedCallback(evt =>
            {
                m_areaPlacementFactor = evt.newValue;
                SavePreferences();
            });
            advancedContent.Add(areaField);

            var repulsionScalingField = new EnumField("Repulsion Scaling", m_repulsionScalingMode)
            {
                tooltip = "How repulsion strength scales with graph distance. Flat/Inverse reduce straight-line tendency in linear branches."
            };
            repulsionScalingField.RegisterValueChangedCallback(evt =>
            {
                m_repulsionScalingMode = (RepulsionScalingMode)evt.newValue;
                SavePreferences();
            });
            advancedContent.Add(repulsionScalingField);

            var bendFactorSlider = new Slider("Bend Factor", 0f, 2f)
            {
                value = m_bendFactor,
                showInputField = true,
                tooltip = "Strength of force that pushes elbow nodes (degree-2) away from straight-line angles. 0 = disabled."
            };
            bendFactorSlider.RegisterValueChangedCallback(evt =>
            {
                m_bendFactor = evt.newValue;
                SavePreferences();
            });
            advancedContent.Add(bendFactorSlider);

            var allowOverlapToggle = new Toggle("Allow Room Overlap") { value = m_allowRoomOverlap };
            allowOverlapToggle.tooltip = "Skip the overlap check and accept whatever the layout produced.";
            allowOverlapToggle.RegisterValueChangedCallback(evt =>
            {
                m_allowRoomOverlap = evt.newValue;
                SavePreferences();
                m_maxRoomRegenerationsField?.SetEnabled(!evt.newValue);
            });
            advancedContent.Add(allowOverlapToggle);

            advancedFoldout.Add(advancedContent);
            advancedFoldout.RegisterValueChangedCallback(evt =>
            {
                m_showStyleAdvanced = evt.newValue;
                SavePreferences();
            });
            section.Add(advancedFoldout);

            return section;
        }

        /// <summary>
        /// Hyperparameters unique to the grid flood-fill layout (displayed to users as "Grid").
        /// Ideal Distance, Force Mode, Chaos Factor, Max Room Regenerations and Max Corridor
        /// Regenerations are shared with every style and live in Basic Settings below instead of
        /// being duplicated here.
        /// </summary>
        private VisualElement BuildFloodFillParameters()
        {
            var section = new BlackboardSection {};
            section.name = "floodfill-parameters";

            var maxCorridorLengthField = new IntegerField("Max Corridor Length")
            {
                value = m_floodFillMaxCorridorLength,
                tooltip = "Longest edge-to-edge distance allowed between two connected rooms. If a " +
                          "layout needs a longer corridor than this, it is re-rolled (see Max Room " +
                          "Regenerations in Basic Settings)."
            };
            maxCorridorLengthField.RegisterValueChangedCallback(evt =>
            {
                m_floodFillMaxCorridorLength = Mathf.Clamp(evt.newValue, 1, 500);
                if (m_floodFillMaxCorridorLength != evt.newValue)
                    maxCorridorLengthField.SetValueWithoutNotify(m_floodFillMaxCorridorLength);
                SavePreferences();
            });
            section.Add(maxCorridorLengthField);

            var backtrackField = new IntegerField("Max Backtrack Attempts")
            {
                value = m_floodFillMaxBacktrackAttempts,
                tooltip = "Grid rings searched outward for a free cell when all 4 sides of a room are " +
                          "already occupied, before that room is deferred to a last-resort placement pass."
            };
            backtrackField.RegisterValueChangedCallback(evt =>
            {
                m_floodFillMaxBacktrackAttempts = Mathf.Clamp(evt.newValue, 1, 64);
                if (m_floodFillMaxBacktrackAttempts != evt.newValue)
                    backtrackField.SetValueWithoutNotify(m_floodFillMaxBacktrackAttempts);
                SavePreferences();
            });
            section.Add(backtrackField);

            var seedField = new IntegerField("Seed")
            {
                value = m_floodFillSeed,
                tooltip = "0 = a new random layout every time you generate (the Console logs which " +
                          "seed was actually used). Any other value reproduces that exact layout every " +
                          "time — lock one in while you tweak other settings, or copy a seed from the " +
                          "Console to get back a layout you liked."
            };
            seedField.RegisterValueChangedCallback(evt =>
            {
                m_floodFillSeed = evt.newValue;
                SavePreferences();
            });
            section.Add(seedField);

            var randomizeSeedBtn = new Button(() =>
            {
                m_floodFillSeed = UnityEngine.Random.Range(1, int.MaxValue);
                seedField.SetValueWithoutNotify(m_floodFillSeed);
                SavePreferences();
            })
            { text = "Randomize Seed" };
            randomizeSeedBtn.tooltip = "Lock in a freshly rolled seed instead of picking a new one on every generation.";
            section.Add(randomizeSeedBtn);

            return section;
        }

        private void BuildNodeSettingsPanel()
        {
            m_nodeSettingsBoard = new Blackboard(this)
            {
                title = "Node Settings",
                subTitle = "No node selected"
            };

            m_nodeSettingsBoard.capabilities |= Capabilities.Movable | Capabilities.Collapsible | Capabilities.Resizable;

            // Size bounds scale with the physical screen, same rationale as the Tools panel.
            Vector2 screen = GetScreenReferenceSize();
            float minWidth     = ScaledDimension(screen.x, 0.11f, 200f, 260f);
            float maxWidth     = ScaledDimension(screen.x, 0.28f, 340f, 560f);
            float defaultWidth = ScaledDimension(screen.x, 0.15f, minWidth, maxWidth);
            float minHeight     = ScaledDimension(screen.y, 0.08f, 80f, 140f);
            float maxHeight     = ScaledDimension(screen.y, 0.45f, 260f, 640f);
            float defaultHeight = ScaledDimension(screen.y, 0.14f, minHeight, maxHeight);

            // Default: top-left corner, as small as content allows
            m_nodeSettingsBoard.SetPosition(new Rect(16, 16, defaultWidth, defaultHeight));
            m_nodeSettingsBoard.style.minWidth  = minWidth;
            m_nodeSettingsBoard.style.minHeight = minHeight;
            m_nodeSettingsBoard.style.maxWidth  = maxWidth;
            m_nodeSettingsBoard.style.maxHeight = maxHeight;

            var addButton = m_nodeSettingsBoard.Q<Button>("addButton");
            if (addButton != null)
                addButton.style.display = DisplayStyle.None;

            ApplyStaticHeaderStyles(m_nodeSettingsBoard);

            // Dedicated inner container cleared on each node selection, avoiding Blackboard.Clear()
            // which disrupts internal event handling and breaks slider dragging.
            var nodeContent = new VisualElement();
            nodeContent.name = "node-settings-content";
            m_nodeSettingsBoard.Add(nodeContent);

            m_nodeSettingsBoard.style.display = DisplayStyle.Flex;
            Add(m_nodeSettingsBoard);
        }

        private void UpdateNodeSettingsPanel(DungeonGraphEditorNode node)
        {
            if (node == null)
                return;

            m_selectedNode = node;
            m_nodeSettingsBoard.subTitle = node.Node.GetType().Name;

            // Clear only the inner content container — never call Blackboard.Clear() because it
            // disrupts internal event routing and prevents slider thumbs from being draggable.
            var nodeContent = m_nodeSettingsBoard.Q("node-settings-content");
            if (nodeContent == null) return;
            nodeContent.Clear();

            int connectionCount = GetNodeConnectionCount(node.Node.id);

            var spawnChanceSection = new VisualElement();
            spawnChanceSection.style.marginTop = 10;
            spawnChanceSection.style.marginBottom = 10;
            spawnChanceSection.style.marginLeft = 5;
            spawnChanceSection.style.marginRight = 5;

            var spawnChanceLabel = new Label("Spawn Chance");
            spawnChanceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            spawnChanceLabel.style.marginBottom = 5;
            spawnChanceSection.Add(spawnChanceLabel);

            var spawnChanceSlider = new Slider("Chance (%)", 0f, 100f)
            {
                value = node.Node.spawnChance,
                showInputField = true
            };
            spawnChanceSlider.RegisterValueChangedCallback(evt =>
            {
                node.Node.spawnChance = evt.newValue;
                EditorUtility.SetDirty(m_dungeonGraph);
                UpdateSpawnChanceWarning(spawnChanceSection, node, connectionCount);
            });
            spawnChanceSection.Add(spawnChanceSlider);

            UpdateSpawnChanceWarning(spawnChanceSection, node, connectionCount);
            nodeContent.Add(spawnChanceSection);
        }

        private void UpdateSpawnChanceWarning(VisualElement container, DungeonGraphEditorNode node, int connectionCount)
        {
            // Remove existing warning if any
            var existingWarning = container.Q<Label>("spawn-chance-warning");
            if (existingWarning != null)
            {
                container.Remove(existingWarning);
            }

            // Add warning if spawn chance < 100 and connections > 2
            if (node.Node.spawnChance < 100f && connectionCount > 2)
            {
                var warningLabel = new Label("⚠ Conditional nodes can only be used with maximum 2 connections");
                warningLabel.name = "spawn-chance-warning";
                warningLabel.style.color = new Color(1f, 0.6f, 0f); // Orange
                warningLabel.style.marginTop = 5;
                warningLabel.style.whiteSpace = WhiteSpace.Normal;
                warningLabel.style.fontSize = 11;
                container.Add(warningLabel);
            }
        }

        private int GetNodeConnectionCount(string nodeId)
        {
            if (m_dungeonGraph.Connections == null) return 0;

            int count = 0;
            foreach (var connection in m_dungeonGraph.Connections)
            {
                if (connection.inputPort.nodeId == nodeId || connection.outputPort.nodeId == nodeId)
                {
                    count++;
                }
            }
            return count;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            float viewWidth = layout.width;
            float viewHeight = layout.height;
            if (viewWidth <= 0 || viewHeight <= 0) return;

            // Dungeon Tools: snap to top-right on first valid layout (layout.width is 0 during construction)
            if (m_toolsBoard != null)
            {
                ShrinkBoardToFitViewport(m_toolsBoard, viewWidth, viewHeight);

                Rect pos = m_toolsBoard.GetPosition();
                float w = pos.width > 0 ? pos.width : 320f;
                float h = pos.height > 0 ? pos.height : 600f;

                if (!m_toolsBoardPositioned)
                {
                    m_toolsBoard.SetPosition(new Rect(viewWidth - w - 16, 16, w, h));
                    m_toolsBoardPositioned = true;
                }
                else
                {
                    bool outOfView = pos.x > viewWidth - 20 || pos.xMax < 20 ||
                                     pos.y > viewHeight - 20 || pos.yMax < 20;
                    if (outOfView)
                        m_toolsBoard.SetPosition(new Rect(viewWidth - w - 16, 16, w, h));
                }
            }

            // Node Settings: respawn at top-left when mostly out of view
            if (m_nodeSettingsBoard != null)
            {
                ShrinkBoardToFitViewport(m_nodeSettingsBoard, viewWidth, viewHeight);

                Rect pos = m_nodeSettingsBoard.GetPosition();
                bool outOfView = pos.x > viewWidth - 20 || pos.xMax < 20 ||
                                 pos.y > viewHeight - 20 || pos.yMax < 20;
                if (outOfView)
                {
                    m_nodeSettingsBoard.SetPosition(new Rect(16, 16, pos.width, pos.height));
                }
            }
        }

        /// <summary>
        /// Shrinks a panel (never grows it) so it fits within the current view, e.g. after the
        /// editor window or a dock split is resized smaller than the panel's current size. This
        /// is what keeps a panel from engulfing the graph view when the window shrinks after the
        /// panel was already sized or manually resized.
        /// </summary>
        private static void ShrinkBoardToFitViewport(Blackboard board, float viewWidth, float viewHeight)
        {
            Rect pos = board.GetPosition();
            float maxW = Mathf.Max(viewWidth - 32f, 0f);
            float maxH = Mathf.Max(viewHeight - 32f, 0f);
            if (pos.width <= maxW && pos.height <= maxH) return;

            float newWidth  = Mathf.Min(pos.width, maxW);
            float newHeight = Mathf.Min(pos.height, maxH);
            board.SetPosition(new Rect(pos.x, pos.y, newWidth, newHeight));
        }

        private void LoadPreferences()
        {
            m_generationStyle = (GenerationStyle)EditorPrefs.GetInt(PREF_GENERATION_STYLE, (int)GenerationStyle.Organic);
            m_showStyleAdvanced = EditorPrefs.GetBool(PREF_SHOW_STYLE_ADVANCED, false);

            m_floodFillMaxCorridorLength = EditorPrefs.GetInt(PREF_FLOODFILL_MAX_CORRIDOR_LENGTH, 40);
            m_floodFillMaxBacktrackAttempts = EditorPrefs.GetInt(PREF_FLOODFILL_MAX_BACKTRACK_ATTEMPTS, 6);
            m_floodFillSeed = EditorPrefs.GetInt(PREF_FLOODFILL_SEED, 0);

            m_areaPlacementFactor = EditorPrefs.GetFloat(PREF_AREA_PLACEMENT, 2.0f);
            m_repulsionFactor = EditorPrefs.GetFloat(PREF_REPULSION, 1.0f);
            m_simulationIterations = EditorPrefs.GetInt(PREF_ITERATIONS, 100);
            m_stiffnessFactor = EditorPrefs.GetFloat(PREF_STIFFNESS, 1.0f);
            m_repulsionScalingMode = (RepulsionScalingMode)EditorPrefs.GetInt(PREF_REPULSION_SCALING, (int)RepulsionScalingMode.Default);
            m_bendFactor = EditorPrefs.GetFloat(PREF_BEND_FACTOR, 0.0f);
            m_realTimeSimulation = EditorPrefs.GetBool(PREF_REALTIME_SIMULATION, false);
            m_simulationSpeed = EditorPrefs.GetFloat(PREF_SIMULATION_SPEED, 10f);
            m_allowRoomOverlap = EditorPrefs.GetBool(PREF_ALLOW_ROOM_OVERLAP, false);

            // Ideal Distance / Chaos Factor / Force Mode / Max Room Regenerations are per-graph,
            // per-style — saved on the DungeonGraphAsset itself (see GenerationStyleTuning)
            // instead of EditorPrefs, so each graph keeps its own tuning per style rather than
            // every graph in the project sharing one global value.
            LoadStyleTuning();

            // Load corridor tile by asset path
            string tilePath = EditorPrefs.GetString(PREF_CORRIDOR_TILE, "");
            if (!string.IsNullOrEmpty(tilePath))
            {
                m_corridorTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>(tilePath);
            }
            m_corridorWidth = EditorPrefs.GetInt(PREF_CORRIDOR_WIDTH, 2);
            m_corridorType = (CorridorType)EditorPrefs.GetInt(PREF_CORRIDOR_TYPE, (int)CorridorType.Direct);
            m_maxCorridorRegenerations = EditorPrefs.GetInt(PREF_MAX_CORRIDOR_REGENERATIONS, 3);

            // Load floor selection
            m_availableFloors = DungeonFloorManager.GetAllFloors();
            string savedFloor = EditorPrefs.GetString(PREF_SELECTED_FLOOR, "");
            if (!string.IsNullOrEmpty(savedFloor) && m_availableFloors.Count > 0)
            {
                m_selectedFloorIndex = m_availableFloors.FindIndex(f => f.folderPath == savedFloor);
                if (m_selectedFloorIndex < 0) m_selectedFloorIndex = 0;
            }
            UpdateCurrentFloorPath();
        }

        private void SavePreferences()
        {
            EditorPrefs.SetInt(PREF_GENERATION_STYLE, (int)m_generationStyle);

            // Each style keeps its own keys so switching styles never loses tuning work
            EditorPrefs.SetBool(PREF_SHOW_STYLE_ADVANCED, m_showStyleAdvanced);

            EditorPrefs.SetInt(PREF_FLOODFILL_MAX_CORRIDOR_LENGTH, m_floodFillMaxCorridorLength);
            EditorPrefs.SetInt(PREF_FLOODFILL_MAX_BACKTRACK_ATTEMPTS, m_floodFillMaxBacktrackAttempts);
            EditorPrefs.SetInt(PREF_FLOODFILL_SEED, m_floodFillSeed);

            EditorPrefs.SetFloat(PREF_AREA_PLACEMENT, m_areaPlacementFactor);
            EditorPrefs.SetFloat(PREF_REPULSION, m_repulsionFactor);
            EditorPrefs.SetInt(PREF_ITERATIONS, m_simulationIterations);
            EditorPrefs.SetFloat(PREF_STIFFNESS, m_stiffnessFactor);
            EditorPrefs.SetInt(PREF_REPULSION_SCALING, (int)m_repulsionScalingMode);
            EditorPrefs.SetFloat(PREF_BEND_FACTOR, m_bendFactor);
            EditorPrefs.SetBool(PREF_REALTIME_SIMULATION, m_realTimeSimulation);
            EditorPrefs.SetFloat(PREF_SIMULATION_SPEED, m_simulationSpeed);
            EditorPrefs.SetBool(PREF_ALLOW_ROOM_OVERLAP, m_allowRoomOverlap);

            SaveStyleTuning();

            // Save corridor tile as asset path
            string tilePath = m_corridorTile != null ? AssetDatabase.GetAssetPath(m_corridorTile) : "";
            EditorPrefs.SetString(PREF_CORRIDOR_TILE, tilePath);
            EditorPrefs.SetInt(PREF_CORRIDOR_WIDTH, m_corridorWidth);
            EditorPrefs.SetInt(PREF_CORRIDOR_TYPE, (int)m_corridorType);
            EditorPrefs.SetInt(PREF_MAX_CORRIDOR_REGENERATIONS, m_maxCorridorRegenerations);

            // Save floor selection
            EditorPrefs.SetString(PREF_SELECTED_FLOOR, m_currentFloorPath);
        }

        /// <summary>
        /// Loads Ideal Distance / Chaos Factor / Force Mode / Max Room Regenerations from this
        /// graph's tuning for the currently selected style. Called on construction and whenever
        /// the selected style changes.
        /// </summary>
        private void LoadStyleTuning()
        {
            if (m_dungeonGraph == null) return;

            var tuning = m_dungeonGraph.GetTuning(m_generationStyle);
            m_idealDistance = tuning.idealDistance;
            m_chaosFactor = tuning.chaosFactor;
            m_forceMode = tuning.forceMode;
            m_maxRoomRegenerations = tuning.maxRoomRegenerations;
        }

        /// <summary>
        /// Writes the current Ideal Distance / Chaos Factor / Force Mode / Max Room
        /// Regenerations values back into this graph's tuning for the currently selected style.
        /// Unlike every other setting, these are not EditorPrefs-backed — they live on the
        /// DungeonGraphAsset itself, per style, so different graphs (and different styles within
        /// the same graph) each keep their own values instead of sharing one global number.
        /// </summary>
        private void SaveStyleTuning()
        {
            if (m_dungeonGraph == null) return;

            var tuning = m_dungeonGraph.GetTuning(m_generationStyle);
            tuning.idealDistance = m_idealDistance;
            tuning.chaosFactor = m_chaosFactor;
            tuning.forceMode = m_forceMode;
            tuning.maxRoomRegenerations = m_maxRoomRegenerations;
            EditorUtility.SetDirty(m_dungeonGraph);
        }

        /// <summary>
        /// Reloads the per-style tuning fields for the newly selected style and pushes them into
        /// the already-built Basic Settings controls (which are not rebuilt on a style switch),
        /// via SetValueWithoutNotify so this doesn't re-trigger their own change callbacks.
        /// </summary>
        private void RefreshStyleTuningControls()
        {
            LoadStyleTuning();

            m_idealDistanceSlider?.SetValueWithoutNotify(m_idealDistance);
            m_chaosSlider?.SetValueWithoutNotify(m_chaosFactor);
            m_forceModeToggle?.SetValueWithoutNotify(m_forceMode);
            m_maxRoomRegenerationsField?.SetValueWithoutNotify(m_maxRoomRegenerations);

            m_simulationIterationsField?.SetEnabled(!m_forceMode);
            m_maxRoomRegenerationsField?.SetEnabled(!m_allowRoomOverlap || m_generationStyle == GenerationStyle.FloodFill);
        }

        private void RefreshFloorList(string selectFloorName)
        {
            m_availableFloors = DungeonFloorManager.GetAllFloors();
            if (selectFloorName != null)
                m_selectedFloorIndex = m_availableFloors.FindIndex(f => f.floorName == selectFloorName);
            m_selectedFloorIndex = Mathf.Clamp(m_selectedFloorIndex, m_availableFloors.Count > 0 ? 0 : -1, m_availableFloors.Count - 1);
            UpdateCurrentFloorPath();
            SavePreferences();
            m_toolsBoard.Clear();
            BuildToolsPanel();
        }

        private void UpdateCurrentFloorPath()
        {
            if (m_availableFloors.Count > 0 && m_selectedFloorIndex >= 0 && m_selectedFloorIndex < m_availableFloors.Count)
            {
                m_currentFloorPath = m_availableFloors[m_selectedFloorIndex].folderPath;
            }
            else
            {
                m_currentFloorPath = "";
            }
        }

        public string GetCurrentFloorPath()
        {
            return m_currentFloorPath;
        }

        /// <summary>
        /// Generate complete dungeon (rooms + corridors + tilemap merge)
        /// </summary>
        private void GenerateDungeon()
        {
            GenerateRooms(generateCorridorsAfterSimulation: true);
            GenerateCorridors();
            //Debug.Log("[DungeonGraphView] Complete dungeon generation finished!");
        }

        /// <summary>
        /// Generate only the rooms with visual connections (no corridors)
        /// </summary>
        private void GenerateRooms(bool generateCorridorsAfterSimulation = false)
        {
            if (m_dungeonGraph == null)
            {
                Debug.LogWarning("[DungeonGraphView] No graph is loaded.");
                return;
            }

            // Destroy previous dungeon if it exists
            var existingDungeon = GameObject.Find("Generated_Dungeon");
            if (existingDungeon != null)
            {
                GameObject.DestroyImmediate(existingDungeon);
                //Debug.Log("[DungeonGraphView] Destroyed previous dungeon.");
            }

            // Reset Master_Tilemap (clear all tiles)
            var masterTilemap = DungeonMasterTilemap.Find();
            if (masterTilemap != null)
            {
                masterTilemap.ClearAllTiles();
            }

            // Work on a copy so the editor asset isn't mutated by runtime logic
            var instance = ScriptableObject.Instantiate(m_dungeonGraph);
            try
            {
                instance.Init();
                var start = instance.GetStartNode();
                if (start == null)
                {
                    Debug.LogError("[DungeonGraphView] No StartNode found in this graph.");
                    return;
                }

                // Generate rooms using the selected generation style. The room factory and
                // bounds provider are style-agnostic — they only resolve prefabs for a node.
                //Debug.Log($"[DungeonGraphView] Starting room generation using {m_generationStyle} method...");

                var roomFactory    = OrganicGenerationEditorBridge.CreateRoomFactory(m_currentFloorPath);
                var boundsProvider = OrganicGenerationEditorBridge.CreateBoundsProvider(m_currentFloorPath);

                if (m_generationStyle == GenerationStyle.FloodFill)
                {
                    DungeonGraph.FloodFillGeneration.GenerateRooms(instance,
                        (node, roomParent) => roomFactory(node, roomParent),
                        node => boundsProvider(node),
                        null,
                        m_idealDistance, m_floodFillMaxCorridorLength, m_floodFillMaxBacktrackAttempts,
                        m_forceMode, m_chaosFactor, m_floodFillSeed,
                        m_maxRoomRegenerations, m_maxCorridorRegenerations)
                        .GetAwaiter().GetResult();
                }
                else
                {
                    DungeonGraph.OrganicGeneration.GenerateRooms(instance, roomFactory, boundsProvider, null,
                        m_areaPlacementFactor, m_repulsionFactor, m_simulationIterations, m_forceMode,
                        m_stiffnessFactor, m_chaosFactor, m_realTimeSimulation, m_simulationSpeed,
                        m_idealDistance, m_allowRoomOverlap, m_maxRoomRegenerations, m_maxCorridorRegenerations,
                        m_repulsionScalingMode, m_bendFactor, generateCorridorsAfterSimulation)
                        .GetAwaiter().GetResult();
                }

                // Assign corridor parameters to the tilemap system
                var generatedDungeon = GameObject.Find("Generated_Dungeon");
                if (generatedDungeon != null)
                {
                    var tilemapSystem = generatedDungeon.GetComponent<DungeonGraph.DungeonTilemapSystem>();
                    if (tilemapSystem != null)
                    {
                        tilemapSystem.corridorTile = m_corridorTile;
                        tilemapSystem.corridorWidth = m_corridorWidth;
                        tilemapSystem.corridorType = m_corridorType;
                        //Debug.Log($"[DungeonGraphView] Assigned corridor tile, width, and type ({m_corridorType}) to tilemap system");
                    }
                }

                //Debug.Log("[DungeonGraphView] Room generation complete!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DungeonGraphView] Room generation failed: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                if (!m_realTimeSimulation)
                {
                    // Keep the processed graph so GenerateCorridors can reuse it —
                    // it has ProcessSpawnChances already applied and bridge connections added.
                    if (m_lastProcessedGraph != null)
                        ScriptableObject.DestroyImmediate(m_lastProcessedGraph);
                    m_lastProcessedGraph = instance;
                }
                // Real-time simulation manages its own instance lifetime via DungeonSimulationController.
            }
        }

        /// <summary>
        /// Generate or regenerate corridors for the existing dungeon
        /// </summary>
        private void GenerateCorridors()
        {
            var existingDungeon = GameObject.Find("Generated_Dungeon");
            if (existingDungeon == null)
            {
                Debug.LogWarning("[DungeonGraphView] No Generated_Dungeon found! Generate rooms first.");
                return;
            }

            // Check if real-time simulation is still running
            var simulationController = existingDungeon.GetComponent<DungeonGraph.DungeonSimulationController>();
            if (simulationController != null)
            {
                //Debug.LogWarning("[DungeonGraphView] Real-time simulation is still running! Wait for simulation to complete before generating corridors.");
                return;
            }

            var tilemapSystem = existingDungeon.GetComponent<DungeonTilemapSystem>();
            if (tilemapSystem == null)
            {
                Debug.LogError("[DungeonGraphView] No DungeonTilemapSystem found on Generated_Dungeon!");
                return;
            }

            // Clear existing corridors before generating new ones
            tilemapSystem.ClearCorridors();

            // Push all corridor settings from Dungeon Tools — these are the single source of truth
            tilemapSystem.corridorTile = m_corridorTile;
            tilemapSystem.corridorWidth = m_corridorWidth;
            tilemapSystem.corridorType = m_corridorType;

            // Try to find master tilemap by tag if not assigned
            if (tilemapSystem.masterTilemap == null)
            {
                if (!tilemapSystem.FindMasterTilemap())
                {
                    Debug.LogError("[DungeonGraphView] Could not find the master tilemap. " +
                                   "Drag Prefabs/Master_Tilemap.prefab into the scene, or add a Dungeon Master Tilemap component to an existing Tilemap.");
                    return;
                }
            }

            if (tilemapSystem.corridorTile == null)
            {
                Debug.LogError("[DungeonGraphView] Corridor tile not assigned! Please assign a corridor tile in Dungeon Tools.");
                return;
            }

            // Use the already-processed graph from the most recent GenerateRooms call so that
            // ProcessSpawnChances results (removed nodes + bridge connections) are preserved.
            // Fall back to a fresh unprocessed copy only if no processed graph is cached yet.
            if (m_lastProcessedGraph != null)
            {
                try
                {
                    GenerateCorridorsForStyle(m_lastProcessedGraph, existingDungeon);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[DungeonGraphView] Corridor generation failed: {ex.Message}\n{ex.StackTrace}");
                }
            }
            else
            {
                var instance = ScriptableObject.Instantiate(m_dungeonGraph);
                try
                {
                    instance.Init();
                    GenerateCorridorsForStyle(instance, existingDungeon);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[DungeonGraphView] Corridor generation failed: {ex.Message}\n{ex.StackTrace}");
                }
                finally
                {
                    if (instance != null)
                        ScriptableObject.DestroyImmediate(instance);
                }
            }
        }

        /// <summary>
        /// Routes corridor generation to the selected generation style.
        /// </summary>
        private void GenerateCorridorsForStyle(DungeonGraphAsset graph, GameObject dungeonParent)
        {
            if (m_generationStyle == GenerationStyle.FloodFill)
                FloodFillGeneration.GenerateCorridors(graph, dungeonParent, m_maxCorridorRegenerations);
            else
                OrganicGeneration.GenerateCorridors(graph, dungeonParent, m_maxCorridorRegenerations);
        }

        /// <summary>
        /// Clear the generated dungeon and all associated data
        /// </summary>
        private void ClearDungeon()
        {
            if (m_lastProcessedGraph != null)
            {
                ScriptableObject.DestroyImmediate(m_lastProcessedGraph);
                m_lastProcessedGraph = null;
            }

            // Destroy the generated dungeon GameObject
            var existingDungeon = GameObject.Find("Generated_Dungeon");
            if (existingDungeon != null)
            {
                GameObject.DestroyImmediate(existingDungeon);
                //Debug.Log("[DungeonGraphView] Cleared generated dungeon.");
            }

            // Clear the master tilemap
            var masterTilemap = DungeonMasterTilemap.Find();
            if (masterTilemap != null)
            {
                masterTilemap.ClearAllTiles();
            }
        }

        // old function for directed ports
        // public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        // {
        //     List<Port> allPorts = new List<Port>();
        //     List<Port> ports = new List<Port>();

        //     foreach (var node in m_graphNodes)
        //     {
        //         allPorts.AddRange(node.Ports);
        //     }

        //     foreach (Port p in allPorts)
        //     {
        //         if (p == startPort) { continue; }
        //         if (p.node == startPort.node) { continue; }
        //         if (p.direction == startPort.direction) { continue; }
        //         if (p.portType == startPort.portType)
        //             ports.Add(p);

        //     }

        //     return ports;
        // }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var result = new List<Port>();
            foreach (var p in ports)
            {
                if (p == startPort) continue;
                if (p.node == startPort.node) continue;                    // no self-loops
                if (p.portType != startPort.portType) continue;            // matching types only
                if (p.direction == startPort.direction) continue;          // require Output↔Input

                // Optional: prevent parallel edges between the same two nodes
                var a = ((DungeonGraphEditorNode)startPort.node).Node.id;
                var b = ((DungeonGraphEditorNode)p.node).Node.id;
                if (HasConnectionBetween(a, b)) continue;

                result.Add(p);
            }
            return result;
        }

        private GraphViewChange OnGraphViewChangedEvent(GraphViewChange graphViewChange)
        {
            if (graphViewChange.movedElements != null)
            {
                Undo.RecordObject(m_serializedObject.targetObject, "Moved Elements");
                foreach (var n in graphViewChange.movedElements.OfType<DungeonGraphEditorNode>())
                {
                    n.SavePosition();
                    // re-aim all edges touching this node
                    // foreach (var p in n.Ports)
                    //     foreach (var e in p.connections)
                    //         ApplyAimTangents(e);
                }
            }
            // If a node got deleted
            if (graphViewChange.elementsToRemove != null)
            {
                List<DungeonGraphEditorNode> nodes = graphViewChange.elementsToRemove.OfType<DungeonGraphEditorNode>().ToList();
                if (nodes.Count > 0)
                {
                    Undo.RecordObject(m_serializedObject.targetObject, "Removed Node");
                    for (int i = nodes.Count - 1; i >= 0; i--)
                    {
                        RemoveNode(nodes[i]);
                    }
                }

                foreach (Edge e in graphViewChange.elementsToRemove.OfType<Edge>())
                {
                    RemoveConnection(e);
                }
            }

            // new connection
            if (graphViewChange.edgesToCreate != null)
            {
                Undo.RecordObject(m_serializedObject.targetObject, "Added Connections");
                foreach (var edge in graphViewChange.edgesToCreate)
                {
                    CreateEdge(edge);                // your persistence
                    // ApplyAimTangents(edge);          // aim now
                    // edge.RegisterCallback<GeometryChangedEvent>(_ => ApplyAimTangents(edge)); // keep aimed on layout changes
                }

                // Update Node Settings if a selected node's connections changed
                if (m_selectedNode != null)
                {
                    UpdateNodeSettingsPanel(m_selectedNode);
                }
            }

            // Update Node Settings when connections are removed
            if (graphViewChange.elementsToRemove != null && graphViewChange.elementsToRemove.OfType<Edge>().Any())
            {
                if (m_selectedNode != null)
                {
                    UpdateNodeSettingsPanel(m_selectedNode);
                }
            }

            return graphViewChange;
        }

        // old directional port code
        // private void CreateEdge(Edge edge)
        // {
        //     DungeonGraphEditorNode inputNode = (DungeonGraphEditorNode)edge.input.node;
        //     int inputIndex = inputNode.Ports.IndexOf(edge.input);

        //     DungeonGraphEditorNode outputNode = (DungeonGraphEditorNode)edge.output.node;
        //     int outputIndex = outputNode.Ports.IndexOf(edge.output);

        //     DungeonGraphConnection connection = new DungeonGraphConnection(inputNode.Node.id, inputIndex, outputNode.Node.id, outputIndex);
        //     m_dungeonGraph.Connections.Add(connection);
        // }

        private void CreateEdge(Edge edge)
        {
            var nodeA = (DungeonGraphEditorNode)edge.output.node; // “output” just means drag start
            var nodeB = (DungeonGraphEditorNode)edge.input.node;

            var aId = nodeA.Node.id;
            var bId = nodeB.Node.id;

            // Canonicalize by GUID so storage is undirected and unique
            DungeonGraphEditorNode leftNode, rightNode;
            Port leftPort, rightPort;

            if (string.CompareOrdinal(aId, bId) <= 0)
            {
                leftNode = nodeA; leftPort = edge.output;
                rightNode = nodeB; rightPort = edge.input;
            }
            else
            {
                leftNode = nodeB; leftPort = edge.input;
                rightNode = nodeA; rightPort = edge.output;
            }

            int leftIndex = Math.Max(0, leftNode.Ports.IndexOf(leftPort));
            int rightIndex = Math.Max(0, rightNode.Ports.IndexOf(rightPort));

            var connection = new DungeonGraphConnection(
                leftNode.Node.id, leftIndex,
                rightNode.Node.id, rightIndex
            );

            m_dungeonGraph.Connections.Add(connection);
            m_connectionDictionary[edge] = connection; // <-- ensure deletions remove from asset
        }

        private void RemoveConnection(Edge e)
        {
            if (m_connectionDictionary.TryGetValue(e, out DungeonGraphConnection connection))
            {
                m_dungeonGraph.Connections.Remove(connection);
                m_connectionDictionary.Remove(e);
            }
        }

        private void RemoveNode(DungeonGraphEditorNode editorNode)
        {
            string nodeId = editorNode.Node.id;

            // Remove all connections that involve this node
            var connectionsToRemove = m_dungeonGraph.Connections
                .Where(c => c.inputPort.nodeId == nodeId || c.outputPort.nodeId == nodeId)
                .ToList();

            // Find and remove the visual edges
            var edgesToRemove = new List<Edge>();
            foreach (var connection in connectionsToRemove)
            {
                foreach (var kvp in m_connectionDictionary)
                {
                    var conn = kvp.Value;
                    if (conn.inputPort.nodeId == connection.inputPort.nodeId &&
                        conn.inputPort.portIndex == connection.inputPort.portIndex &&
                        conn.outputPort.nodeId == connection.outputPort.nodeId &&
                        conn.outputPort.portIndex == connection.outputPort.portIndex)
                    {
                        edgesToRemove.Add(kvp.Key);
                        break;
                    }
                }
            }

            // Remove connections from data
            foreach (var connection in connectionsToRemove)
            {
                m_dungeonGraph.Connections.Remove(connection);
            }

            // Remove visual edges
            foreach (var edge in edgesToRemove)
            {
                m_connectionDictionary.Remove(edge);
                edge.RemoveFromHierarchy();
            }

            // Remove the node itself
            m_dungeonGraph.Nodes.Remove(editorNode.Node);
            m_nodeDictionary.Remove(nodeId);
            m_graphNodes.Remove(editorNode);

            // Force serialization update
            m_serializedObject.Update();
            EditorUtility.SetDirty(m_dungeonGraph);
        }

        private void DrawNodes()
        {
            foreach (DungeonGraphNode node in m_dungeonGraph.Nodes)
            {
                AddNodeToGraph(node);
            }

            Bind();
        }

        private void DrawConnections()
        {
            if (m_dungeonGraph.Connections == null) { return; }
            foreach (DungeonGraphConnection connection in m_dungeonGraph.Connections)
            {
                DrawConnection(connection);
            }
        }

        private void DrawConnection(DungeonGraphConnection connection)
        {
            // Look up the two nodes the connection references
            DungeonGraphEditorNode inputNode = GetNode(connection.inputPort.nodeId);
            DungeonGraphEditorNode outputNode = GetNode(connection.outputPort.nodeId);
            if (inputNode == null || outputNode == null) return;
            if (inputNode.Ports == null || inputNode.Ports.Count == 0) return;
            if (outputNode.Ports == null || outputNode.Ports.Count == 0) return;

            // Clamp indices in case older assets still point past end (you already do this)
            int inIdx = Mathf.Clamp(connection.inputPort.portIndex, 0, inputNode.Ports.Count - 1);
            int outIdx = Mathf.Clamp(connection.outputPort.portIndex, 0, outputNode.Ports.Count - 1);

            // Grab the saved ports
            Port a = inputNode.Ports[inIdx];
            Port b = outputNode.Ports[outIdx];

            // Figure out which end is Output and which is Input
            Port outEnd = null, inEnd = null;
            DungeonGraphEditorNode outNode = null, inNode = null;

            if (a.direction == Direction.Output && b.direction == Direction.Input)
            {
                outEnd = a; inEnd = b; outNode = inputNode; inNode = outputNode;
            }
            else if (a.direction == Direction.Input && b.direction == Direction.Output)
            {
                outEnd = b; inEnd = a; outNode = outputNode; inNode = inputNode;
            }
            else
            {
                // Fallback: pick the correct jacks from each node regardless of saved indices
                // (nodes should always have exactly one visible Output + one Input)
                var aOut = inputNode.Ports.FirstOrDefault(p => p.direction == Direction.Output);
                var aIn = inputNode.Ports.FirstOrDefault(p => p.direction == Direction.Input);
                var bOut = outputNode.Ports.FirstOrDefault(p => p.direction == Direction.Output);
                var bIn = outputNode.Ports.FirstOrDefault(p => p.direction == Direction.Input);

                // Prefer InputNode as Input and OutputNode as Output
                if (aOut != null && bIn != null)
                {
                    outEnd = aOut; inEnd = bIn; outNode = inputNode; inNode = outputNode;
                }
                else if (bOut != null && aIn != null)
                {
                    outEnd = bOut; inEnd = aIn; outNode = outputNode; inNode = inputNode;
                }
                else
                {
                    // Nothing to do if we still can’t resolve ends
                    return;
                }
            }

            // Always connect Output -> Input (prevents "same direction" exception)
            var edge = outEnd.ConnectTo(inEnd);
            AddElement(edge);

            // Keep the view/data map in sync so deletes work later
            m_connectionDictionary[edge] = connection;

            // ApplyAimTangents(edge);
            // edge.RegisterCallback<GeometryChangedEvent>(_ => ApplyAimTangents(edge));

            int newInIdx = (inNode == inputNode) ? inputNode.Ports.IndexOf(inEnd) : outputNode.Ports.IndexOf(inEnd);
            int newOutIdx = (outNode == outputNode) ? outputNode.Ports.IndexOf(outEnd) : inputNode.Ports.IndexOf(outEnd);

            bool changed = false;
            if (newInIdx != connection.inputPort.portIndex) { connection.inputPort.portIndex = newInIdx; changed = true; }
            if (newOutIdx != connection.outputPort.portIndex) { connection.outputPort.portIndex = newOutIdx; changed = true; }
            if (changed) EditorUtility.SetDirty(m_dungeonGraph);
        }

        private DungeonGraphEditorNode GetNode(string nodeId)
        {
            DungeonGraphEditorNode node = null;
            m_nodeDictionary.TryGetValue(nodeId, out node);
            return node;
        }

        private void ShowSearchWindow(NodeCreationContext context)
        {
            m_searchProvider.target = (VisualElement)focusController.focusedElement;
            SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), m_searchProvider);
        }

        public void Add(DungeonGraphNode node)
        {
            Undo.RecordObject(m_serializedObject.targetObject, "Added Node");

            m_dungeonGraph.Nodes.Add(node);

            m_serializedObject.Update();

            AddNodeToGraph(node);
            Bind();
        }

        private void AddNodeToGraph(DungeonGraphNode node)
        {
            node.typeName = node.GetType().AssemblyQualifiedName;

            DungeonGraphEditorNode editorNode = new DungeonGraphEditorNode(node, m_serializedObject);
            editorNode.SetPosition(node.position);
            m_graphNodes.Add(editorNode);
            m_nodeDictionary.Add(node.id, editorNode);
            AddElement(editorNode);
        }

        private void Bind()
        {
            //Debug.Log("Bind");
            m_serializedObject.Update();
            this.Bind(m_serializedObject);
        }

        private bool HasConnectionBetween(string aId, string bId)
        {
            if (m_dungeonGraph.Connections == null) return false;
            foreach (var c in m_dungeonGraph.Connections)
            {
                var x = c.inputPort.nodeId;
                var y = c.outputPort.nodeId;
                if ((x == aId && y == bId) || (x == bId && y == aId))
                    return true;
            }
            return false;
        }

        private DungeonGraphEditorNode _dragStartNode;
        private Direction _dragStartDirection;

        private void OnGlobalMouseDown(MouseDownEvent evt)
        {
            var port = (evt.target as VisualElement)?.GetFirstAncestorOfType<Port>();
            if (port == null) return;

            // We only allow drags to start from the visible Output jack
            if (port.direction != Direction.Output) return;

            _dragStartNode = (DungeonGraphEditorNode)port.node;
            _dragStartDirection = Direction.Output;

            // While dragging from an Output, only Inputs on other nodes are droppable
            SetPickabilityDuringDrag(true, _dragStartNode, _dragStartDirection);
        }

        private void OnGlobalMouseUp(MouseUpEvent evt)
        {
            // Drag ended or canceled: restore defaults (Output grabbable, Input ignored)
            SetPickabilityDuringDrag(false, null, Direction.Output);
            _dragStartNode = null;
        }

        private void SetPickabilityDuringDrag(bool dragging, DungeonGraphEditorNode startNode, Direction startDir)
        {
            foreach (var n in m_graphNodes)
            {
                foreach (var p in n.Ports)
                {
                    if (!dragging)
                    {
                        // Default: you can only grab Outputs; Inputs are not pickable until a drag starts
                        p.pickingMode = (p.direction == Direction.Output) ? PickingMode.Position : PickingMode.Ignore;
                        continue;
                    }

                    // Dragging from an Output on startNode?
                    if (startDir == Direction.Output)
                    {
                        if (n == startNode)
                        {
                            // Keep this node's Output pickable so the edge connector keeps the drag alive
                            p.pickingMode = (p.direction == Direction.Output) ? PickingMode.Position : PickingMode.Ignore;
                        }
                        else
                        {
                            // Other nodes: only Inputs are droppable
                            p.pickingMode = (p.direction == Direction.Input) ? PickingMode.Position : PickingMode.Ignore;
                        }
                    }
                }
            }
        }
        
        // Tune this to taste or expose it in your window; pixels at 100% zoom.
        private const float TangentPixels = 100f;

        private void ApplyAimTangents(Edge edge)
        {
            if (edge?.output == null || edge.input == null) return;

            // EdgeControl works in contentViewContainer space
            Vector2 from = contentViewContainer.WorldToLocal(edge.output.worldBound.center);
            Vector2 to   = contentViewContainer.WorldToLocal(edge.input.worldBound.center);

            Vector2 dir = (to - from);
            float dist = dir.magnitude;
            if (dist < 0.001f) dist = 0.001f;
            dir /= dist;

            //float strength = Mathf.Min(TangentPixels, dist * 0.5f);
            float strength = TangentPixels;

            var ec = edge.edgeControl;
            ec.from = from;
            ec.to   = to;

            // ⬇️ Mutate the existing control points array instead of assigning a new one
            var cps = ec.controlPoints;
            if (cps == null || cps.Length < 2)
            {
                // EdgeControl may not have allocated yet; try again on next layout tick
                edge.schedule.Execute(() => ApplyAimTangents(edge));
                return;
            }

            cps[0] = from + dir * strength;  // start tangent
            cps[1] = to   - dir * strength;  // end tangent

            ec.MarkDirtyRepaint();
        }

        // Re-aim all edges touching a node (call on move/geometry change)
        private void ReaimEdgesForNode(DungeonGraphEditorNode node)
        {
            if (node == null) return;
            foreach (var port in node.Ports)
            {
                // Port.connections is IEnumerable<Edge>
                foreach (var e in port.connections)
                    ApplyAimTangents(e);
            }
        }

        // ===== COPY/PASTE FUNCTIONALITY =====

        private string SerializeGraphElementsCallback(IEnumerable<GraphElement> elements)
        {
            var nodes = elements.OfType<DungeonGraphEditorNode>().Select(n => n.Node).ToList();

            // Create a wrapper ScriptableObject to leverage Unity's serialization
            var wrapper = ScriptableObject.CreateInstance<CopyPasteWrapper>();
            wrapper.nodes = nodes;

            string json = EditorJsonUtility.ToJson(wrapper, true);
            ScriptableObject.DestroyImmediate(wrapper);

            return json;
        }

        private void UnserializeAndPasteCallback(string operationName, string data)
        {
            // Create a wrapper to deserialize into
            var wrapper = ScriptableObject.CreateInstance<CopyPasteWrapper>();
            EditorJsonUtility.FromJsonOverwrite(data, wrapper);

            if (wrapper?.nodes == null || wrapper.nodes.Count == 0)
            {
                ScriptableObject.DestroyImmediate(wrapper);
                return;
            }

            // Offset for pasted nodes
            Vector2 pasteOffset = new Vector2(50, 50);

            Undo.RecordObject(m_serializedObject.targetObject, "Paste Nodes");

            foreach (var originalNode in wrapper.nodes)
            {
                if (originalNode == null)
                    continue;

                // Create a new instance - this automatically generates a new GUID
                if (!(Activator.CreateInstance(originalNode.GetType()) is DungeonGraphNode newNode))
                    continue;

                // Copy field values using reflection to preserve node-specific data
                CopyNodeFields(originalNode, newNode);

                // Offset position
                var oldPos = originalNode.position;
                newNode.SetPosition(new Rect(oldPos.position + pasteOffset, oldPos.size));

                // Add to graph
                m_dungeonGraph.Nodes.Add(newNode);

                // Update serialized object BEFORE creating editor node
                // This ensures the serialized property can be found in DungeonGraphEditorNode constructor
                m_serializedObject.Update();

                AddNodeToGraph(newNode);
            }

            ScriptableObject.DestroyImmediate(wrapper);
            m_serializedObject.Update();

            // Bind the serialized object to ensure property fields are connected
            Bind();

            EditorUtility.SetDirty(m_dungeonGraph);
        }

        private void CopyNodeFields(DungeonGraphNode source, DungeonGraphNode destination)
        {
            // Get all fields from the source node's type hierarchy
            var type = source.GetType();
            while (type != null && type != typeof(object))
            {
                var fields = type.GetFields(System.Reflection.BindingFlags.Public |
                                           System.Reflection.BindingFlags.NonPublic |
                                           System.Reflection.BindingFlags.Instance |
                                           System.Reflection.BindingFlags.DeclaredOnly);

                foreach (var field in fields)
                {
                    // Skip guid and position fields - we handle those separately
                    if (field.Name == "m_guid" || field.Name == "m_position")
                        continue;

                    // Skip readonly/const fields
                    if (field.IsLiteral || field.IsInitOnly)
                        continue;

                    try
                    {
                        var value = field.GetValue(source);
                        field.SetValue(destination, value);
                    }
                    catch
                    {
                        // Skip fields that can't be copied
                    }
                }

                type = type.BaseType;
            }
        }

        private bool CanPasteSerializedDataCallback(string data)
        {
            try
            {
                var wrapper = ScriptableObject.CreateInstance<CopyPasteWrapper>();
                EditorJsonUtility.FromJsonOverwrite(data, wrapper);
                bool canPaste = wrapper?.nodes != null && wrapper.nodes.Count > 0;
                ScriptableObject.DestroyImmediate(wrapper);
                return canPaste;
            }
            catch
            {
                return false;
            }
        }

        private class CopyPasteWrapper : ScriptableObject
        {
            [SerializeReference]
            public List<DungeonGraphNode> nodes = new List<DungeonGraphNode>();
        }
    }
}
