using UnityEngine;
using UnityEngine.UI;

namespace Inspection
{
    public enum BannerKind
    {
        Info,
        Success,
        Error
    }

    public sealed class InspectionUI
    {
        public InputField Title;
        public InputField Description;
        public InputField BaseUrl;
        public Text ArStatus;
        public Text Position;
        public Text Banner;
        public Image BannerBackground;
        public Button Submit;
        public Button TestConnection;
        public RectTransform Panel;
        public Canvas Canvas;
        public string Priority = "medium";

        readonly Button lowButton;
        readonly Button mediumButton;
        readonly Button highButton;

        InspectionUI(Button low, Button medium, Button high)
        {
            lowButton = low;
            mediumButton = medium;
            highButton = high;
        }

        public bool IsTyping
        {
            get
            {
                return (Title != null && Title.isFocused) ||
                       (Description != null && Description.isFocused) ||
                       (BaseUrl != null && BaseUrl.isFocused);
            }
        }

        public void TickKeyboard()
        {
            if (Panel == null || Canvas == null)
                return;
            var shift = 0f;
            if (TouchScreenKeyboard.visible)
            {
                var scale = Canvas.scaleFactor <= 0f ? 1f : Canvas.scaleFactor;
                shift = TouchScreenKeyboard.area.height / scale;
            }
            Panel.anchoredPosition = new Vector2(0f, shift);
        }

        public void SetPriority(string value)
        {
            if (value != "low" && value != "medium" && value != "high")
                value = "medium";
            Priority = value;
            PaintPriority(lowButton, value == "low");
            PaintPriority(mediumButton, value == "medium");
            PaintPriority(highButton, value == "high");
        }

        public void SetBanner(string message, BannerKind kind)
        {
            if (Banner != null)
                Banner.text = message ?? "";
            if (BannerBackground == null)
                return;
            switch (kind)
            {
                case BannerKind.Success:
                    BannerBackground.color = new Color(0.15f, 0.42f, 0.30f, 1f);
                    break;
                case BannerKind.Error:
                    BannerBackground.color = new Color(0.55f, 0.18f, 0.16f, 1f);
                    break;
                default:
                    BannerBackground.color = new Color(0.16f, 0.20f, 0.24f, 1f);
                    break;
            }
        }

        public void SetBusy(bool busy)
        {
            var on = !busy;
            if (Submit != null) Submit.interactable = on;
            if (TestConnection != null) TestConnection.interactable = on;
            if (Title != null) Title.interactable = on;
            if (Description != null) Description.interactable = on;
            if (BaseUrl != null) BaseUrl.interactable = on;
            if (lowButton != null) lowButton.interactable = on;
            if (mediumButton != null) mediumButton.interactable = on;
            if (highButton != null) highButton.interactable = on;
        }

        public static InspectionUI Build()
        {
            var font = BuiltinFont();
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var eventGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            eventGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            var top = new GameObject("AR Status", typeof(RectTransform), typeof(Image));
            top.transform.SetParent(canvasGo.transform, false);
            var topRt = top.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.sizeDelta = new Vector2(0f, 210f);
            topRt.anchoredPosition = Vector2.zero;
            top.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.09f, 0.92f);
            var arStatus = CreateStretchText(top.transform, font, 32, FontStyle.Normal, TextAnchor.MiddleLeft);
            arStatus.text = "正在启动 AR…";

            var panelGo = new GameObject("Report Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(0f, 0f);
            panelGo.GetComponent<Image>().color = new Color(0.09f, 0.10f, 0.11f, 0.94f);
            var layout = panelGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 22, 28);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = panelGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var position = CreateLabel(panelGo.transform, font, 28, 72);
            position.text = "尚未放置标记。点击检测到的水平面。";
            CreateLabel(panelGo.transform, font, 26, 40).text = "标题（必填）";
            var title = CreateInput(panelGo.transform, font, "例如：入口墙面破损", false);
            CreateLabel(panelGo.transform, font, 26, 40).text = "描述（可选）";
            var description = CreateInput(panelGo.transform, font, "例如：左侧墙体存在裂缝", true);
            CreateLabel(panelGo.transform, font, 26, 40).text = "优先级";

            var row = new GameObject("Priority", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(panelGo.transform, false);
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;
            var rowLe = row.GetComponent<LayoutElement>();
            rowLe.minHeight = 84;
            rowLe.preferredHeight = 84;
            var low = CreateButton(row.transform, "低", font, new Color(0.22f, 0.24f, 0.26f, 1f));
            var medium = CreateButton(row.transform, "中", font, new Color(0.22f, 0.24f, 0.26f, 1f));
            var high = CreateButton(row.transform, "高", font, new Color(0.22f, 0.24f, 0.26f, 1f));

            CreateLabel(panelGo.transform, font, 26, 40).text = "后端地址";
            var url = CreateInput(panelGo.transform, font, "http://192.168.0.10:8787", false);

            var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            buttons.transform.SetParent(panelGo.transform, false);
            var buttonLayout = buttons.GetComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 12;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = true;
            var buttonLe = buttons.GetComponent<LayoutElement>();
            buttonLe.minHeight = 90;
            buttonLe.preferredHeight = 90;
            var test = CreateButton(buttons.transform, "测试连接", font, new Color(0.28f, 0.32f, 0.36f, 1f));
            var submit = CreateButton(buttons.transform, "提交问题", font, new Color(0.12f, 0.48f, 0.46f, 1f));

            var bannerGo = new GameObject("Banner", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            bannerGo.transform.SetParent(panelGo.transform, false);
            var bannerLe = bannerGo.GetComponent<LayoutElement>();
            bannerLe.minHeight = 110;
            bannerLe.preferredHeight = 140;
            var bannerBg = bannerGo.GetComponent<Image>();
            var banner = CreateStretchText(bannerGo.transform, font, 30, FontStyle.Normal, TextAnchor.MiddleLeft);

            var ui = new InspectionUI(low, medium, high)
            {
                Title = title,
                Description = description,
                BaseUrl = url,
                ArStatus = arStatus,
                Position = position,
                Banner = banner,
                BannerBackground = bannerBg,
                Submit = submit,
                TestConnection = test,
                Panel = panelRt,
                Canvas = canvas
            };
            low.onClick.AddListener(() => ui.SetPriority("low"));
            medium.onClick.AddListener(() => ui.SetPriority("medium"));
            high.onClick.AddListener(() => ui.SetPriority("high"));
            ui.SetPriority("medium");
            ui.SetBanner("填写标题后提交。描述可以留空。", BannerKind.Info);
            return ui;
        }

        static void PaintPriority(Button button, bool selected)
        {
            if (button == null)
                return;
            var image = button.GetComponent<Image>();
            if (image == null)
                return;
            image.color = selected
                ? new Color(0.12f, 0.48f, 0.46f, 1f)
                : new Color(0.22f, 0.24f, 0.26f, 1f);
        }

        static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        static Text CreateLabel(Transform parent, Font font, int size, float height)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            var text = go.GetComponent<Text>();
            ApplyText(text, font, size, FontStyle.Normal, TextAnchor.MiddleLeft);
            text.color = new Color(0.86f, 0.88f, 0.86f, 1f);
            return text;
        }

        static Text CreateStretchText(Transform parent, Font font, int size, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(24f, 12f);
            rt.offsetMax = new Vector2(-24f, -12f);
            var text = go.GetComponent<Text>();
            ApplyText(text, font, size, style, anchor);
            return text;
        }

        static void ApplyText(Text text, Font font, int size, FontStyle style, TextAnchor anchor)
        {
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
        }

        static InputField CreateInput(Transform parent, Font font, string placeholder, bool multiline)
        {
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = multiline ? 140 : 88;
            le.preferredHeight = multiline ? 140 : 88;
            var image = go.GetComponent<Image>();
            image.color = new Color(0.16f, 0.18f, 0.2f, 1f);

            var text = CreateInsetText(go.transform, font, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            var hint = CreateInsetText(go.transform, font, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            hint.color = new Color(1f, 1f, 1f, 0.38f);
            hint.text = placeholder;
            hint.fontStyle = FontStyle.Italic;

            var input = go.GetComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.customCaretColor = true;
            input.caretColor = Color.white;
            input.selectionColor = new Color(0.2f, 0.55f, 0.52f, 0.45f);
            return input;
        }

        static Text CreateInsetText(Transform parent, Font font, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(18f, 10f);
            rt.offsetMax = new Vector2(-18f, -10f);
            var text = go.GetComponent<Text>();
            ApplyText(text, font, 32, FontStyle.Normal, anchor);
            return text;
        }

        static Button CreateButton(Transform parent, string label, Font font, Color background)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 84;
            le.preferredHeight = 84;
            le.flexibleWidth = 1f;
            var image = go.GetComponent<Image>();
            image.color = background;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var text = CreateStretchText(go.transform, font, 32, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label;
            var rt = text.GetComponent<RectTransform>();
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return button;
        }
    }
}
