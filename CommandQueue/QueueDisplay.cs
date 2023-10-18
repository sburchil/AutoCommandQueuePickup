using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AutoCommandQueuePickup.Hooks;
using AutoCommandQueuePickup.Configuration;

namespace AutoCommandQueuePickup.CommandQueue
{
    public class QueueDisplay : MonoBehaviour
    {
        public GameObject itemIconPrefab = null;
        public float itemIconPrefabWidth = 64f;
        public ItemTier tier;
        private float iconScale = 1;
        private RectTransform rectTransform;
        private List<ItemIcon> icons = new List<ItemIcon>();
        private List<QueueManager.QueueEntry> queue;

        public void Awake()
        {
            rectTransform = transform as RectTransform;
        }

        public void Start()
        {
            iconScale = (rectTransform.rect.height - 10) / itemIconPrefabWidth;
            LayoutIcons();
        }

        public void LayoutIcon(int index, RectTransform transform)
        {
            transform.anchoredPosition = new Vector2(index * iconScale * (itemIconPrefabWidth + 5), 5);
            transform.localScale = new Vector3(iconScale, iconScale, 1);
        }

        public void LayoutIcons(int first = 0)
        {
            for (int i = first; i < icons.Count; i++)
            {
                LayoutIcon(i, icons[i].rectTransform);
            }
        }

        public ItemIcon AllocateIcon(int index)
        {
            ItemIcon icon = Instantiate(itemIconPrefab, rectTransform, false).GetComponent<ItemIcon>();
            RectTransform transform = icon.rectTransform;

            Button button = icon.gameObject.AddComponent<Button>();
            button.onClick.AddListener(delegate
            {
                for (int i = 0; i < icons.Count; i++)
                {
                    if (icons[i] == icon)
                    {
                        QueueManager.Remove(tier, i);
                        break;
                    }
                }
            });

            button.navigation = new Navigation
            {
                mode = Navigation.Mode.None,
            };
            transform.anchorMin = new Vector2(0, 1);
            transform.anchorMax = new Vector2(0, 1);
            LayoutIcon(index, transform);

            QueueItemBehavior queueItemBehavior = icon.gameObject.AddComponent<QueueItemBehavior>();
            queueItemBehavior.Init(this);

            icons.Insert(index, icon);

            return icon;
        }

        public void AllocateIcons(int count, Action<int, ItemIcon> action = null)
        {
            for (int i = icons.Count - 1; i >= count; i--)
            {
                Destroy(icons[i].gameObject);
                icons.RemoveAt(i);
            }

            for (int i = icons.Count; i < count; i++)
            {
                ItemIcon icon = AllocateIcon(i);

                action?.Invoke(i, icon);
            }
        }

        public void DestroyUI()
        {
            foreach (var icon in icons)
            {
                Destroy(icon.gameObject);
            }
            icons.Clear();
        }

        public void UpdateUI()
        {
            AllocateIcons(queue.Count);
            UpdateIcons();
        }

        private void UpdateIcon(int i)
        {
            ItemIcon icon = icons[i];
            var entry = queue[i];
            icon.SetItemIndex(PickupCatalog.GetPickupDef(entry.pickupIndex).itemIndex, entry.count);
        }

        private void UpdateIcons()
        {
            for (int i = 0; i < icons.Count; i++)
            {
                UpdateIcon(i);
            }
        }

        private void HandleQueueChange(QueueManager.QueueChange change, ItemTier tier, int index)
        {
            if (!itemIconPrefab)
                return;
            if (tier == this.tier)
            {
                switch (change)
                {
                    case QueueManager.QueueChange.Changed:
                        UpdateIcon(index);
                        break;
                    case QueueManager.QueueChange.Added:
                        AllocateIcon(index);
                        UpdateIcon(index);
                        break;
                    case QueueManager.QueueChange.Removed:
                        Destroy(icons[index].gameObject);
                        icons.RemoveAt(index);
                        LayoutIcons(index);
                        break;
                    case QueueManager.QueueChange.Moved:
                        UpdateUI();
                        break;
                }
            }
        }

        public void OnEnable()
        {
            if (!itemIconPrefab)
                return;
            queue = QueueManager.mainQueues[tier];
            UpdateUI();
            QueueManager.OnQueueChanged += HandleQueueChange;
        }

        public void OnDisable()
        {
            QueueManager.OnQueueChanged -= HandleQueueChange;
        }

        // Code based on example from https://docs.unity3d.com/2018.1/Documentation/ScriptReference/EventSystems.IDragHandler.html
        public class QueueItemBehavior : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
        {
            private GameObject m_DraggingIcon;
            private QueueDisplay m_Queue;
            private ItemIcon m_ItemIcon;

            public void Init(QueueDisplay queue)
            {
                m_Queue = queue;
                m_ItemIcon = GetComponent<ItemIcon>();
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                switch (eventData.button)
                {
                    case PointerEventData.InputButton.Left:
                    case PointerEventData.InputButton.Right:
                        break;
                    default: return;
                }

                m_DraggingIcon = new GameObject("QueueItemDragIcon");

                m_DraggingIcon.transform.SetParent(m_Queue.transform, false);
                m_DraggingIcon.transform.SetAsLastSibling();

                var image = m_DraggingIcon.AddComponent<RawImage>();

                image.texture = m_ItemIcon.image.texture;
                //image.SetNativeSize();
                var imageTransform = image.rectTransform;
                var size = m_ItemIcon.image.rectTransform.rect.height;

                imageTransform.anchorMin = new Vector2(0.5f, 0.5f);
                imageTransform.anchorMax = new Vector2(0.5f, 0.5f);
                imageTransform.sizeDelta = new Vector2(size, size);

                imageTransform.localScale = m_ItemIcon.rectTransform.localScale;

                image.color = new Color(1f, 1f, 1f, 0.6f);

                SetDraggedPosition(eventData);
                eventData.Use();
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (m_DraggingIcon)
                {
                    SetDraggedPosition(eventData);
                    eventData.Use();
                }
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (!m_DraggingIcon) return;

                Destroy(m_DraggingIcon);

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Queue.rectTransform, eventData.position, eventData.pressEventCamera, out var localPos))
                {
                    float fractionalIndex = localPos.x / (m_Queue.iconScale * (m_Queue.itemIconPrefabWidth + 5));
                    int newIndex = Mathf.RoundToInt(fractionalIndex);

                    int index = m_Queue.icons.FindIndex(icon => icon.gameObject == gameObject);
                    var queue = m_Queue.queue;
                    if (index < 0 || index > queue.Count)
                        return;
                    var entry = m_Queue.queue[index];
                    int count = entry.count;
                    if (eventData.button == PointerEventData.InputButton.Right && count > 1)
                        count /= 2;

                    QueueManager.Move(m_Queue.tier, index, newIndex, count);
                }
                eventData.Use();
            }

            private void SetDraggedPosition(PointerEventData data)
            {
                var rt = m_DraggingIcon.GetComponent<RectTransform>();
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(m_Queue.rectTransform, data.position, data.pressEventCamera, out var globalMousePos))
                {
                    rt.position = globalMousePos;
                }
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Right && AutoCommandQueuePickup.ModConfig.rightClickRemovesStack.Value)
                {
                    var icons = m_Queue.icons;
                    for (int i = 0; i < icons.Count; i++)
                    {
                        if (icons[i].gameObject == gameObject)
                        {
                            var entry = m_Queue.queue[i];
                            QueueManager.Remove(m_Queue.tier, i, entry.count);
                            break;
                        }
                    }
                    eventData.Use();
                }
            }
        }
    }
}
