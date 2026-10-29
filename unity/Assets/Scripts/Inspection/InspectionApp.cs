using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Inspection
{
    // Place this on an otherwise empty scene. It creates the AR rig, the report
    // form, and submits issues to the Go API. See the repository README.
    public sealed class InspectionApp : MonoBehaviour
    {
        public string backendBaseUrl = BackendConfig.DefaultBaseUrl;

        InspectionUI ui;
        ArRig rig;
        MarkerPlacer placer;
        readonly IssueApiClient api = new IssueApiClient();
        GameObject selected;
        bool submitting;

        void Awake()
        {
            ui = InspectionUI.Build();
            ui.BaseUrl.text = BackendConfig.Load(backendBaseUrl);
            ui.BaseUrl.onEndEdit.AddListener(BackendConfig.Save);
            ui.Submit.onClick.AddListener(Submit);
            ui.TestConnection.onClick.AddListener(TestConnection);

            try
            {
                rig = ArRigBuilder.Build();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                ui.ArStatus.text = "AR 初始化失败。请确认已安装 AR Foundation，并用真机运行。";
                ui.SetBanner("AR 初始化失败：" + ex.Message, BannerKind.Error);
                return;
            }

            placer = new MarkerPlacer(rig.Camera, rig.Raycast, rig.Planes, rig.MarkerRoot);
            placer.IsTyping = () => ui.IsTyping;
            placer.CanPlace = () => !submitting;
            placer.OnPlaced = OnPlaced;
            placer.OnMiss = message => ui.Position.text = message;
        }

        void Update()
        {
            if (ui != null)
            {
                ui.TickKeyboard();
                ui.ArStatus.text = DescribeAr();
            }
            if (placer != null)
                placer.Tick();
        }

        void OnPlaced(Vector3 position, GameObject marker)
        {
            selected = marker;
            ui.Position.text = FormatPosition(position);
            ui.SetBanner("已放置标记。填写标题后点「提交问题」。", BannerKind.Info);
        }

        void Submit()
        {
            if (submitting || ui == null)
                return;
            var title = ui.Title.text == null ? "" : ui.Title.text.Trim();
            if (selected == null)
            {
                ui.SetBanner("请先点击已检测的水平面放置标记。", BannerKind.Error);
                return;
            }
            if (title.Length == 0)
            {
                ui.SetBanner("标题不能为空。", BannerKind.Error);
                return;
            }

            var baseUrl = BackendConfig.Normalize(ui.BaseUrl.text);
            if (baseUrl.Length == 0)
            {
                ui.SetBanner("请填写后端地址，例如 http://192.168.0.10:8787", BannerKind.Error);
                return;
            }
            BackendConfig.Save(baseUrl);
            ui.BaseUrl.text = baseUrl;

            var marker = selected;
            var position = marker.transform.position;
            var priority = ui.Priority;
            var description = ui.Description.text == null ? "" : ui.Description.text.Trim();
            submitting = true;
            ui.SetBusy(true);
            ui.SetBanner("正在提交…", BannerKind.Info);
            StartCoroutine(api.CreateIssue(baseUrl, new IssueCreateRequest
            {
                title = title,
                description = description,
                priority = priority,
                position = new IssuePosition { x = position.x, y = position.y, z = position.z }
            }, issue =>
            {
                submitting = false;
                ui.SetBusy(false);
                MarkerPlacer.SetColor(marker, MarkerPlacer.ColorForPriority(priority));
                if (selected == marker)
                    selected = null;
                ui.Title.text = "";
                ui.Description.text = "";
                ui.Position.text = "已保存。可以继续点击平面放置下一个标记。";
                ui.SetBanner("提交成功。" + issue.title + "    id " + issue.id + "    状态 " + issue.status, BannerKind.Success);
            }, error =>
            {
                submitting = false;
                ui.SetBusy(false);
                MarkerPlacer.SetColor(marker, new Color(0.65f, 0.16f, 0.14f, 1f));
                ui.SetBanner("提交失败：" + error, BannerKind.Error);
            }));
        }

        void TestConnection()
        {
            if (submitting || ui == null)
                return;
            var baseUrl = BackendConfig.Normalize(ui.BaseUrl.text);
            if (baseUrl.Length == 0)
            {
                ui.SetBanner("请填写后端地址。", BannerKind.Error);
                return;
            }
            BackendConfig.Save(baseUrl);
            ui.BaseUrl.text = baseUrl;
            ui.SetBusy(true);
            ui.SetBanner("正在连接 " + baseUrl + " …", BannerKind.Info);
            StartCoroutine(api.CheckHealth(baseUrl, () =>
            {
                ui.SetBusy(false);
                ui.SetBanner("后端连接成功。", BannerKind.Success);
            }, error =>
            {
                ui.SetBusy(false);
                ui.SetBanner("连接失败：" + error, BannerKind.Error);
            }));
        }

        string DescribeAr()
        {
            if (rig == null)
                return "AR 未就绪。";

            if (Application.isEditor && ARSession.state != ARSessionState.SessionTracking)
                return "编辑器里没有真实 AR。点击画面会在相机前方放一个标记，只用来试提交。平面检测需要真机。";

            var planes = placer != null ? placer.PlaneCount : 0;
            switch (ARSession.state)
            {
                case ARSessionState.None:
                case ARSessionState.CheckingAvailability:
                    return "正在检查这台设备是否支持 AR…";
                case ARSessionState.NeedsInstall:
                    return "需要安装或更新 Google Play 服务 for AR（ARCore）。";
                case ARSessionState.Installing:
                    return "正在安装 AR 组件…";
                case ARSessionState.Ready:
                case ARSessionState.SessionInitializing:
                    return "AR 正在启动。请允许相机权限。";
                case ARSessionState.SessionTracking:
                    if (planes > 0)
                        return "已检测到水平面（" + planes + "）。点击平面放置标记。";
                    return "AR 跟踪中。缓慢移动手机，扫描地面或桌面。";
                case ARSessionState.Unsupported:
                    return "这台设备不支持 AR。";
                default:
                    return "AR 状态：" + ARSession.state;
            }
        }

        static string FormatPosition(Vector3 position)
        {
            return "标记位置    x " + position.x.ToString("0.00") +
                   "    y " + position.y.ToString("0.00") +
                   "    z " + position.z.ToString("0.00");
        }
    }
}
