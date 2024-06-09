using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AutoCommandQueuePickup
{
    class UIManager : MonoBehaviour
    {
        private static readonly GameObject commandUIPrefab;
        private static readonly GameObject commandCancelButton;
        private static readonly PickupPickerPanel prefabPickupPickerPanel;
        private static readonly GameObject commandItemButton;
        private static readonly GameObject commandItemContainer;
        private static Texture2D repeatIconTexture;

        //private static readonly MethodInfo PickupPickerPanel_Awake = typeof(PickupPickerPanel).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo PickupPickerController_GetOptionsFromPickupIndex = typeof(PickupPickerController).GetMethod("GetOptionsFromPickupIndex", BindingFlags.Static | BindingFlags.NonPublic);

        static UIManager()
        {
            commandUIPrefab = RoR2.Artifacts.CommandArtifactManager.commandCubePrefab.GetComponent<PickupPickerController>().panelPrefab;
            commandCancelButton = commandUIPrefab.GetComponentsInChildren<HGButton>().First(a => a.name == "CancelButton").gameObject;
            prefabPickupPickerPanel = commandUIPrefab.GetComponent<PickupPickerPanel>();
            commandItemButton = prefabPickupPickerPanel.buttonPrefab;
            commandItemContainer = prefabPickupPickerPanel.buttonContainer.gameObject;
        }

        private struct ButtonWrapper
        {
            public HGButton hgButton
            {
                get;
                private set;
            }
            public TextMeshProUGUI textComponent
            {
                get;
                private set;
            }
            public RectTransform rectTransform
            {
                get;
                private set;
            }
            public LanguageTextMeshController languageTextMeshController
            {
                get;
                private set;
            }

            public ButtonWrapper(HGButton button)
            {
                hgButton = button;
                textComponent = button.GetComponentInChildren<TextMeshProUGUI>();
                rectTransform = button.GetComponent<RectTransform>();
                languageTextMeshController = button.GetComponent<LanguageTextMeshController>();
            }

            public Button.ButtonClickedEvent _onClick => hgButton.onClick;

            public event UnityAction onClick
            {
                add => _onClick.AddListener(value);
                remove => _onClick.RemoveListener(value);
            }

            public string text
            {
                get => languageTextMeshController.token;
                set => languageTextMeshController.token = value;
            }

            public string directText
            {
                get => textComponent?.text;
                set => textComponent.text = value;
            }
        }

        private static void CopyRectTransform(RectTransform dest, RectTransform source)
        {
            dest.anchorMin = source.anchorMin;
            dest.anchorMax = source.anchorMax;
            dest.anchoredPosition = source.anchoredPosition;
            dest.position = source.position;
            dest.sizeDelta = source.sizeDelta;
        }

        private static void ReparentChildren(Transform dest, Transform source, bool keepWorldPos = false, Action<Transform> additionalAction = null)
        {
            List<Transform> children = new List<Transform>();
            foreach(Transform child in source)
            {
                if(child != dest)
                    children.Add(child);
            }
            foreach(var child in children)
            {
                child.SetParent(dest, keepWorldPos);
                additionalAction?.Invoke(child);
            }
        }

        private static System.Collections.IEnumerator ExecuteNextFrameInner(Action action)
        {
            yield return 0;
            action();
        }

        private void ExecuteNextFrame(Action action)
        {
            StartCoroutine(ExecuteNextFrameInner(action));
        }

        private ButtonWrapper CreateButton(GameObject parent = null, UnityAction onClick = null, string text = null)
        {
            var buttonObject = Instantiate(commandCancelButton, parent?.transform);
            uiElements.Add(buttonObject);
            HGButton button = buttonObject.GetComponent<HGButton>();

            button.onSelect = new UnityEvent();
            button.onDeselect = new UnityEvent();
            button.onClick = new Button.ButtonClickedEvent();

            ButtonWrapper wrappedButton = new ButtonWrapper(button);

            if (text != null) wrappedButton.text = text;

            if (onClick != null) wrappedButton.onClick += onClick;

            return wrappedButton;
        }
        
        private static readonly Color selectedColorMult = new Color(0.3f, 0.3f, 0.3f);

        private Sprite missingUnlockIcon;
        private GameObject itemIconPrefab;
        private ItemInventoryDisplay itemInventoryDisplayPrefab;
        private ScoreboardController scoreboard;
        private HUD hud;

        private GameObject container;
        private GameObject origContainer;

        private ColorBlock origButtonColors;
        private ColorBlock selectedButtonColors;
        private ButtonWrapper scoreboardButton;
        private ButtonWrapper queueButton;

        private ItemTier lastActiveTab = ItemTier.Tier1;
        private GameObject queueContainer;
        private readonly Dictionary<ItemTier, GameObject> queueDisplays = new Dictionary<ItemTier, GameObject>();
        private readonly Dictionary<ItemTier, ButtonWrapper> queueButtons = new Dictionary<ItemTier, ButtonWrapper>();
        private bool displayingQueues;
        private bool DisplayingQueues
        {
            get => displayingQueues;
            set
            {
                if (displayingQueues != value)
                {
                    if (origContainer && queueContainer)
                    {
                        if (value)
                        {
                            container.SetActive(false);
                            queueContainer.SetActive(true);
                        }
                        else
                        {
                            container.SetActive(true);
                            queueContainer.SetActive(false);
                        }
                    }
                    displayingQueues = value;
                    UpdateButtonColors();
                }
            }
        }
        private ItemTier selectedQueue = ItemTier.Tier1;
        private ItemTier SelectedQueue
        {
            get => selectedQueue;
            set
            {
                if(selectedQueue != value)
                {
                    queueDisplays[selectedQueue].SetActive(false);
                    queueDisplays[value].SetActive(true);
                    TransitionEnabledButton(queueButtons[selectedQueue], queueButtons[value]);

                    selectedQueue = value;
                }
            }
        }

        private void MarkButton(ButtonWrapper button, bool state)
        {
            button.hgButton.colors = state ? selectedButtonColors : origButtonColors;
            button.hgButton.imageOnHover.color = state ? selectedColorMult : Color.white;
        }
        
        private void TransitionEnabledButton(ButtonWrapper from, ButtonWrapper to)
        {
            MarkButton(from, false);
            MarkButton(to, true);
        }

        private void UpdateButtonColors()
        {
            ButtonWrapper enabledButton, disabledButton;

            if (!(queueButton.hgButton && scoreboardButton.hgButton))
                return;

            if(DisplayingQueues)
            {
                enabledButton = queueButton;
                disabledButton = scoreboardButton;
            }
            else
            {
                enabledButton = scoreboardButton;
                disabledButton = queueButton;
            }

            TransitionEnabledButton(disabledButton, enabledButton);
        }

        private void PrintObject(GameObject obj, bool components = false, int depth = 0)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(' ', depth*2);
            sb.Append(obj.ToString());
            Debug.Log(sb.ToString());
            if(components)
            {
                foreach(var component in obj.GetComponents<Component>())
                {
                    sb.Clear();
                    sb.Append(' ', depth * 2);
                    sb.Append('-');
                    sb.Append(component.ToString());
                    Debug.Log(sb.ToString());
                }
            }
            foreach (Transform inner in obj.transform)
            {
                PrintObject(inner.gameObject, components, depth + 1);
            }
        }

        private PickupIndex GetPickupIndexOfItemTier(ItemTier itemTier)
        {
            List<PickupIndex> pickupIndices = null;
            switch(itemTier)
            {
                case ItemTier.Tier1:
                    pickupIndices = Run.instance.availableTier1DropList;
                    break;
                case ItemTier.Tier2:
                    pickupIndices = Run.instance.availableTier2DropList;
                    break;
                case ItemTier.Tier3:
                    pickupIndices = Run.instance.availableTier3DropList;
                    break;
                case ItemTier.Boss:
                    pickupIndices = Run.instance.availableBossDropList;
                    break;
                case ItemTier.Lunar:
                    pickupIndices = Run.instance.availableLunarItemDropList;
                    break;
                case ItemTier.VoidTier1:
                    pickupIndices = Run.instance.availableVoidTier1DropList;
                    break;
                case ItemTier.VoidTier2:
                    pickupIndices = Run.instance.availableVoidTier2DropList;
                    break;
                case ItemTier.VoidTier3:
                    pickupIndices = Run.instance.availableVoidTier3DropList;
                    break;
                case ItemTier.VoidBoss:
                    pickupIndices = Run.instance.availableVoidBossDropList;
                    break;
            }

            if (pickupIndices == null)
                return PickupIndex.none;

            return pickupIndices.FirstOrDefault();
        }

        private RectTransform CreateQueueContainer(ItemTier itemTier, RectTransform parent, Action<PickupIndex> callback)
        {
            GameObject gameObject = new GameObject($"{itemTier}QueueContainer", typeof(RectTransform));
            RectTransform transform = gameObject.transform as RectTransform;
            transform.SetParent(parent, false);
            
            GameObject queueDisplay = Instantiate(itemInventoryDisplayPrefab.gameObject, transform, false);
            DestroyImmediate(queueDisplay.GetComponent<ItemInventoryDisplay>());
            DestroyImmediate(queueDisplay.GetComponent<LayoutElement>());
            RectTransform queueTransform = queueDisplay.transform as RectTransform;
            
            queueTransform.anchorMin = new Vector2(0, 1);
            queueTransform.anchorMax = new Vector2(1, 1);
            queueTransform.sizeDelta = new Vector2(-60, 50);
            queueTransform.anchoredPosition = new Vector2(0, -5);
            queueTransform.pivot = new Vector2(0, 1);

            ButtonWrapper repeatButton = default; // So we can reference it in the delegate
            repeatButton = CreateButton(text: "", parent: gameObject, onClick: delegate
            {
                QueueManager.ToggleRepeat(itemTier);
                MarkButton(repeatButton, QueueManager.DoesRepeat(itemTier));
            });

            RectTransform repeatTransform = repeatButton.rectTransform;
            repeatTransform.anchorMin = new Vector2(1, 1);
            repeatTransform.anchorMax = new Vector2(1, 1);
            repeatTransform.sizeDelta = new Vector2(55, 55);
            repeatTransform.anchoredPosition = new Vector2(0, -2.5f);
            repeatTransform.pivot = new Vector2(1, 1);

            GameObject repeatButtonImageHolder = new GameObject($"{itemTier}RepeatButtonImageHolder", typeof(RectTransform));
            RectTransform repeatButtonImageHolderTransform = repeatButtonImageHolder.transform as RectTransform;
            repeatButtonImageHolderTransform.SetParent(repeatTransform, false);
            repeatButtonImageHolderTransform.anchorMin = new Vector2(0, 0);
            repeatButtonImageHolderTransform.anchorMax = new Vector2(1, 1);
            repeatButtonImageHolderTransform.sizeDelta = new Vector2(-15, -15);
            RawImage repeatImage = repeatButtonImageHolder.AddComponent<RawImage>();
            repeatImage.texture = repeatIconTexture;

            MarkButton(repeatButton, QueueManager.DoesRepeat(itemTier));
            
            var queueDisplayComponent = queueDisplay.AddComponent<QueueDisplay>();
            queueDisplayComponent.itemIconPrefab = itemIconPrefab;
            queueDisplayComponent.itemIconPrefabWidth = itemInventoryDisplayPrefab.itemIconPrefabWidth;
            queueDisplayComponent.tier = itemTier;

            var itemButtonsContainer = Instantiate(commandItemContainer, transform, false);
            var itemButtonsTransform = itemButtonsContainer.transform as RectTransform;
            var gridLayoutGroup = itemButtonsContainer.GetComponent<GridLayoutGroup>();

            var buttonAllocator = new UIElementAllocator<MPButton>(itemButtonsTransform, commandItemButton);
            if (AutoCommandQueuePickup.config.bigItemButtonContainer.Value)
            {
                Destroy(itemButtonsContainer.GetComponent<RawImage>());
                //PrintObject(gameObject, true);
                gridLayoutGroup.constraint = GridLayoutGroup.Constraint.Flexible;
                gridLayoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
                gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
                gridLayoutGroup.childAlignment = TextAnchor.UpperCenter;
                gridLayoutGroup.cellSize *= AutoCommandQueuePickup.config.bigItemButtonScale.Value;
                
                //ExecuteNextFrame(delegate
                //{
                    itemButtonsTransform.anchoredPosition = new Vector2(0, 0); // If somebody can tell me why exactly this doesn't work if done immediately, please tell me
                    itemButtonsTransform.anchorMin = new Vector2(0, 0);
                    itemButtonsTransform.anchorMax = new Vector2(1, 1);
                    itemButtonsTransform.sizeDelta = new Vector2(0, -55);
                    itemButtonsTransform.pivot = new Vector2(0.5f, 0);
                //});
            }
            else
            {
                gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayoutGroup.constraintCount = prefabPickupPickerPanel.maxColumnCount;
                gridLayoutGroup.childAlignment = TextAnchor.UpperCenter;
                itemButtonsTransform.anchorMin = new Vector2(0.5f, 1);
                itemButtonsTransform.anchorMax = new Vector2(0.5f, 1);
                itemButtonsTransform.anchoredPosition = new Vector2(0, -55);
                itemButtonsTransform.pivot = new Vector2(0, 1);
                itemButtonsTransform.sizeDelta = new Vector2();
            }


            var itemOptions = (PickupPickerController.Option[])PickupPickerController_GetOptionsFromPickupIndex.Invoke(null, new object[] { GetPickupIndexOfItemTier(itemTier) });

            if (itemOptions != null)
            {
                buttonAllocator.onCreateElement = (index, button) =>
                {
                    var option = itemOptions[index];
                    var pickupDef = PickupCatalog.GetPickupDef(option.pickupIndex);
                    Image component = button.GetComponent<ChildLocator>().FindChild("Icon").GetComponent<Image>();
                    if (option.available && pickupDef != null)
                    {
                        component.color = Color.white;
                        component.sprite = pickupDef.iconSprite;
                        button.interactable = true;

                        PickupIndex pickupIndex = pickupDef.pickupIndex;
                        button.onClick.AddListener(delegate
                        {
                            callback(pickupIndex);
                            LayoutRebuilder.MarkLayoutForRebuild(itemButtonsTransform);
                        });
                    }
                    else
                    {
                        component.color = Color.gray;
                        component.sprite = missingUnlockIcon;
                        button.interactable = false;
                    }
                };
                buttonAllocator.AllocateElements(itemOptions.Length);
            }

            //Rect whiteItemRect = itemButtonsTransform.rect;
            //itemButtonsTransform.anchorMin = new Vector2(0.5f, 0);
            //itemButtonsTransform.anchorMax = new Vector2(0.5f, 0);

            return transform;
        }

        private readonly List<GameObject> uiElements = new List<GameObject>();

        private void DestroyUI()
        {
            foreach (var element in uiElements)
                Destroy(element);
            uiElements.Clear();

            if(container != null)
            {
                RectTransform containerTransform = container.GetComponent<RectTransform>();
                RectTransform origTransform = origContainer.GetComponent<RectTransform>();

                ReparentChildren(origTransform, containerTransform);

                Destroy(container);
                Destroy(queueContainer);
                container = null;
            }
        }
        
        private void RebuildUI()
        {
            DestroyUI();

            origButtonColors = commandCancelButton.GetComponent<HGButton>().colors;
            var selectedColors = origButtonColors;
            selectedColors.disabledColor *= selectedColorMult;
            selectedColors.pressedColor *= selectedColorMult;

            selectedColors.highlightedColor = selectedColors.normalColor = new Color(0.7f, 0.55f, 0.1f, 1f);

            selectedButtonColors = selectedColors;

            #region Container setup

            container = new GameObject("CommandQueueScoreboardWrapper", typeof(RectTransform));
            RectTransform containerTransform = container.GetComponent<RectTransform>();

            origContainer = scoreboard.gameObject;
            RectTransform origTransform = origContainer.GetComponent<RectTransform>();

            containerTransform.SetParent(origTransform, false);
            containerTransform.sizeDelta = new Vector2(0, 0);
            containerTransform.anchorMin = new Vector2(0, 0);
            containerTransform.anchorMax = new Vector2(1, 0.9f);
            
            ReparentChildren(containerTransform, origTransform);
            #endregion
            #region Queue container setup 
            queueContainer = new GameObject("CommandQueueContainer", typeof(RectTransform));
            RectTransform queueTransform = queueContainer.transform as RectTransform;

            queueTransform.SetParent(origTransform, false);
            CopyRectTransform(queueTransform, containerTransform);

            Dictionary<ItemTier, string> tierNameOverrides = new Dictionary<ItemTier, string> { { ItemTier.Tier1, "White" }, { ItemTier.Tier2, "Green" }, { ItemTier.Tier3, "Red" } };

            object[] tiers = AutoCommandQueuePickup.config.enabledTabsConfig.Where(a => a.Value).Select(a => Enum.Parse(typeof(ItemTier), a.Definition.Key)).ToArray();
            float tabCount = (float)tiers.Length + 1;
            int i = 0;
            foreach (ItemTier tier in tiers)
            {
                string name = tierNameOverrides.ContainsKey(tier) ? tierNameOverrides[tier] : tier.ToString();
                ButtonWrapper button = CreateButton(text: name, parent: queueContainer, onClick: delegate
                {
                    SelectedQueue = tier;
                });

                button.rectTransform.anchorMin = new Vector2(i / tabCount, 8f/9f);
                button.rectTransform.anchorMax = new Vector2((i + 1) / tabCount, 1);
                button.rectTransform.anchoredPosition = new Vector2();
                button.rectTransform.sizeDelta = new Vector2();

                RectTransform queueDisplay = CreateQueueContainer(tier, queueTransform, index =>
                {
                    QueueManager.Enqueue(index);
                });

                queueDisplay.anchorMin = new Vector2(0, 0);
                queueDisplay.anchorMax = new Vector2(1, 8f/9f);
                queueDisplay.sizeDelta = new Vector2();
                queueDisplay.anchoredPosition = new Vector2();
                queueDisplay.gameObject.SetActive(false);
                button.onClick += () =>
                {
                    lastActiveTab = tier;
                };

                queueDisplays[tier] = queueDisplay.gameObject;
                queueButtons[tier] = button;
                i++;
            }

            selectedQueue = lastActiveTab;
            queueDisplays[selectedQueue].SetActive(true);
            ExecuteNextFrame(delegate {
                MarkButton(queueButtons[selectedQueue], true);
            });

            queueContainer.SetActive(false);
            #endregion
            #region Button setup

            scoreboardButton = CreateButton(text: "Scoreboard", parent: origContainer);

            scoreboardButton.rectTransform.anchoredPosition = new Vector2();
            scoreboardButton.rectTransform.anchorMin = new Vector2(0, 0.9f);
            scoreboardButton.rectTransform.anchorMax = new Vector2(0.333f, 1);
            scoreboardButton.rectTransform.sizeDelta = new Vector2();

            scoreboardButton.onClick += () =>
            {
                DisplayingQueues = false;
            };

            queueButton = CreateButton(text: "Command Queue", parent: origContainer);

            queueButton.rectTransform.anchoredPosition = new Vector2();
            queueButton.rectTransform.anchorMin = new Vector2(0.333f, 0.9f);
            queueButton.rectTransform.anchorMax = new Vector2(0.667f, 1);
            queueButton.rectTransform.sizeDelta = new Vector2();

            queueButton.onClick += () =>
            {
                DisplayingQueues = true;
            };
            #endregion

            ExecuteNextFrame(() => UpdateButtonColors());
        }

        public void Awake()
        {
            if (!AutoCommandQueuePickup.IsLoaded)
            {
                Destroy(this);
                return;
            }
            repeatIconTexture = new Texture2D(0, 0);

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AutoCommandQueuePickup.repeat-solid.png"))
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                byte[] data = memoryStream.ToArray();
                repeatIconTexture.LoadImage(data);
            }
            scoreboard = GetComponent<ScoreboardController>();
            hud = GetComponentInParent<HUD>();
            missingUnlockIcon = Resources.Load<Sprite>("Textures/MiscIcons/texUnlockIcon");
            itemInventoryDisplayPrefab = scoreboard.stripPrefab.GetComponent<ScoreboardStrip>().itemInventoryDisplay;
            itemIconPrefab = itemInventoryDisplayPrefab.itemIconPrefab;
            //PrintObject(scoreboard.stripPrefab.GetComponent<ScoreboardStrip>().itemInventoryDisplay.gameObject, true);
            AutoCommandQueuePickup.PluginUnloaded += () => DestroyImmediate(this);
        }

        private void OnDestroy()
        {
            DestroyUI();
            Destroy(repeatIconTexture);
        }

        private void OnEnable()
        {
            if (container == null)
                RebuildUI();
        }
    }
}
