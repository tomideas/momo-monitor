using System.ComponentModel;

namespace StatusMonitor.I18n;

/// <summary>
/// Tiny localization dictionary. Bind with
/// {Binding Source={x:Static i18n:Loc.Instance}, Path=[key]}.
/// Setting the language raises PropertyChanged(null) so every binding refreshes.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private string _lang = "zh";
    private Loc() { }

    public string Language => _lang;

    public void SetLanguage(string lang)
    {
        _lang = lang == "en" ? "en" : "zh";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public string this[string key]
    {
        get
        {
            if (Map.TryGetValue(key, out var v)) return _lang == "zh" ? v.zh : v.en;
            return key;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly Dictionary<string, (string en, string zh)> Map = new()
    {
        ["app_title"] = ("Momo System Monitor", "Momo 系統監測"),
        ["general"] = ("General", "一般"),
        ["theme"] = ("Background", "背景配色"),
        ["theme_sky"] = ("Sky blue", "晴空藍"),
        ["theme_lime"] = ("Electric lime", "螢光黃綠"),
        ["theme_orange"] = ("Tangerine", "活力橙"),
        ["theme_violet"] = ("Lavender", "薰衣草紫"),
        ["theme_mint"] = ("Fresh green", "鮮薄荷綠"),
        ["theme_paper"] = ("Soft white", "柔和白"),
        ["theme_night"] = ("Deep blue", "深海藍"),
        ["mini"] = ("Mini", "迷你"),
        ["restore"] = ("Open dashboard", "開啟儀表板"),
        ["exit"] = ("Exit", "結束程式"),
        ["pin"] = ("Always on top", "保持置頂"),
        ["mini_topmost"] = ("Keep the mini panel on top of other windows", "迷你面板保持置頂於其他視窗"),
        ["start_mini"] = ("Start in mini mode", "啟動時開啟迷你模式"),
        ["period_today"] = ("Today", "今日"),
        ["period_week"] = ("7 days", "7 天"),
        ["period_month"] = ("30 days", "30 天"),
        ["period_all"] = ("All", "全部"),
        ["record_since"] = ("daily record starts {0}", "每日記錄自 {0} 起"),
        ["record_empty"] = ("daily record starts today", "每日記錄從今天開始"),
        ["confirm_reset"] = ("Tap again to reset", "再按一次確認重置"),
        ["group_appearance"] = ("Appearance", "外觀"),
        ["group_energy"] = ("Energy & rates", "用電與費率"),
        ["group_window"] = ("Window & processes", "視窗與程序"),
        ["ask_on_close"] = ("Ask what to do when the window is closed", "關閉視窗時詢問"),
        ["close_title"] = ("Close Momo System Monitor", "關閉 Momo 系統監測"),
        ["close_body"] = (
            "Hide the window and keep sampling in the tray, or exit completely?",
            "僅關閉視窗並在系統匣繼續採集，還是完全退出？"),
        ["close_hide"] = ("Hide to tray", "僅關閉視窗"),
        ["close_exit"] = ("Exit completely", "完全退出"),
        ["close_dont_ask"] = ("Don't ask again", "下次不再詢問"),
        ["run_in_tray"] = ("Keep running in the tray when closed or minimized",
                          "關閉或最小化時留在系統匣繼續執行"),
        ["tray_hint"] = ("Off by default: the close button ends the program. The tray icon's right-click menu always offers Exit.",
                         "預設關閉：按右上角 ✕ 會直接結束程式。系統匣圖示右鍵選單永遠有「結束程式」。"),
        ["trends"] = ("TRENDS", "歷史趨勢"),
        ["trend_hint"] = ("This session · hover to inspect · missing readings leave gaps", "本次開啟期間 · 滑過查看數值 · 缺失讀數保留空段"),
        ["trend_wait"] = ("Waiting for sensor readings…", "等待感測器讀數…"),
        ["now"] = ("Now", "現在"),
        ["minimum"] = ("Min", "最低"),
        ["average"] = ("Avg", "平均"),
        ["maximum"] = ("Max", "最高"),
        ["primary_gpu"] = ("Primary GPU", "主 GPU"),
        ["gpu_auto"] = ("Automatic · prefer dedicated GPU", "自動 · 優先獨立顯卡"),
        ["gpu_unavailable"] = ("Saved GPU unavailable · using automatic", "原選 GPU 不可用 · 暫用自動選擇"),
        ["hide_integrated"] = ("Hide integrated GPU when a dedicated GPU is available", "有獨立顯卡時隱藏內顯"),
        ["integrated"] = ("Integrated", "內顯"),
        ["shared_memory"] = ("Shared", "共享記憶體"),
        ["gpu_memory_usage"] = ("GPU memory %", "GPU 記憶體 %"),
        ["monitoring"] = ("Monitoring & alerts", "監控與提醒"),
        ["alerts_enabled"] = ("Notify on sustained threshold breaches", "持續超過門檻時通知"),
        ["cpu_limit"] = ("CPU temperature · °C", "CPU 溫度 · °C"),
        ["gpu_limit"] = ("GPU temperature · °C", "GPU 溫度 · °C"),
        ["ram_limit"] = ("Memory usage · %", "記憶體使用率 · %"),
        ["vram_limit"] = ("GPU memory usage · %", "GPU 記憶體使用率 · %"),
        ["disk_limit"] = ("Disk free space below · %", "磁碟剩餘空間低於 · %"),
        ["hold_seconds"] = ("Sustained duration · seconds", "持續時間 · 秒"),
        ["cooldown_seconds"] = ("Notification cooldown · seconds", "通知冷卻時間 · 秒"),
        ["alert_hint"] = ("One notice per incident. GPU alerts follow your primary GPU. Missing readings never trigger alerts.", "每次異常只通知一次，GPU 提醒跟隨主 GPU；缺失讀數不觸發提醒。"),
        ["apply_monitoring"] = ("Apply alert settings", "套用提醒設定"),
        ["saved"] = ("Saved", "已儲存"),
        ["invalid_alerts"] = ("Use temperatures 1–150, percentages 1–100, duration 1–600 and cooldown 0–3600 seconds.", "溫度請填 1–150、百分比 1–100、持續時間 1–600 秒、冷卻 0–3600 秒。"),
        ["alerts"] = ("RECENT ALERTS", "最近提醒"),
        ["no_alerts"] = ("No alerts this session", "本次開啟尚無提醒"),
        ["free_space"] = ("free space", "剩餘空間"),
        ["live"] = ("LIVE", "即時"),
        ["stale"] = ("Readings delayed", "讀數延遲"),
        ["details"] = ("View details  →", "查看詳情  →"),
        ["copy_specs"] = ("Copy specifications", "複製規格"),
        ["copied"] = ("Copied", "已複製"),
        ["info_hint"] = ("Your hardware, at a glance. Open a card for full specifications.", "硬體規格，一眼掌握。查看詳情可閱讀完整資訊。"),
        ["reduce_motion"] = ("Reduce motion", "減少動效"),
        ["refresh"] = ("Refresh", "重新整理"),
        ["cpu"] = ("CPU", "處理器"),
        ["gpu"] = ("GPU", "顯卡"),
        ["ram"] = ("RAM", "記憶體"),
        ["available"] = ("available", "可用"),
        ["used"] = ("Used", "已用"),
        ["approx"] = ("approx.", "約略值"),
        ["memory_type"] = ("Memory type", "記憶體類型"),
        ["memory_shared"] = ("Shared with system memory", "與系統記憶體共享"),
        ["memory_dedicated"] = ("Dedicated", "專用"),
        ["drag_hint"] = ("Drag anywhere to move · double-click to open the dashboard", "任意處拖曳移動 · 雙擊開啟儀表板"),
        ["theme_volt"] = ("Volt (dark)", "Volt 夜跑"),
        ["theme_paper"] = ("Paper Pop (light)", "Paper Pop 日間"),
        ["network"] = ("Network", "網路"),
        ["storage"] = ("Storage", "儲存"),
        ["fans"] = ("Fans & Pump", "風扇與幫浦"),
        ["load"] = ("Load", "負載"),
        ["temperature"] = ("Temperature", "溫度"),
        // The dashboard detail lines are 11 DIP mono and already carry three or four readings,
        // so they use the short form. Alert names keep the full word, where it reads as a
        // sentence rather than a column.
        ["temp_short"] = ("Temp", "溫度"),
        ["clock"] = ("Clock", "時脈"),
        ["fan"] = ("Fan", "風扇"),
        ["power"] = ("Power", "功率"),
        ["top_processes"] = ("Top Processes", "熱門處理程序"),
        ["col_process"] = ("Process", "處理程序"),
        ["col_cpu"] = ("CPU", "CPU"),
        ["col_gpu"] = ("GPU", "GPU"),
        ["col_ram"] = ("RAM", "記憶體"),
        ["col_power"] = ("Power", "瓦數"),
        ["col_disk"] = ("Disk I/O", "磁碟 I/O"),
        ["col_download"] = ("Download", "下載"),
        ["col_upload"] = ("Upload", "上傳"),
        ["power_now"] = ("Estimated power", "估算功耗"),
        ["energy_total"] = ("Energy", "累計用電"),
        ["carbon"] = ("Carbon", "碳足跡"),
        ["cost"] = ("Cost", "電費"),
        ["settings"] = ("Settings", "設定"),
        ["language"] = ("Language", "語言"),
        ["font"] = ("Font", "字型"),
        ["font_default"] = ("Built-in Geist + Noto Sans TC", "內建 Geist + Noto Sans TC"),
        ["font_jhenghei"] = ("Microsoft JhengHei", "微軟正黑體"),
        ["font_segoe"] = ("Segoe UI", "Segoe UI"),
        ["font_mingliu"] = ("PMingLiU", "細明體"),
        ["country"] = ("Country / Region", "國家／地區"),
        ["electric_price"] = ("Electricity price", "電價"),
        ["carbon_intensity"] = ("Carbon intensity", "碳強度"),
        ["process_count"] = ("Processes shown", "顯示程序數"),
        ["currency"] = ("Currency", "幣別"),
        ["custom"] = ("Custom", "自訂"),
        ["reset_totals"] = ("Reset totals", "重置累計"),
        ["close"] = ("Close", "關閉"),
        ["per_kwh"] = ("/kWh", "/kWh"),
        ["g_per_kwh"] = ("g/kWh", "g/kWh"),
        ["no_data"] = ("N/A", "無資料"),
        ["admin_required"] = ("Sensor driver unavailable — run as administrator for temperatures/fans/power.",
                              "感測驅動不可用 — 以管理員身分執行才能顯示溫度/風扇/瓦數。"),
        ["reading"] = ("Reading…", "讀取中…"),
        ["nav_dashboard"] = ("Dashboard", "儀表板"),
        ["nav_info"] = ("Info", "信息"),
        ["info_cpu"] = ("CPU", "CPU"),
        ["info_gpu"] = ("GPU", "GPU"),
        ["info_ram"] = ("Memory", "記憶體"),
        ["info_system"] = ("System", "系統"),
        ["info_storage"] = ("Storage", "儲存"),
        ["info_battery"] = ("Battery", "電池"),
        ["info_display"] = ("Display", "顯示器"),
        ["model"] = ("Model", "型號"),
        ["cores"] = ("Cores", "核心"),
        ["core_threads"] = ("{0} cores / {1} threads", "{0} 核心 / {1} 執行緒"),
        ["total_memory"] = ("Total memory", "總記憶體"),
        ["swap"] = ("Swap", "Swap"),
        ["os"] = ("Operating system", "作業系統"),
        ["hostname"] = ("Host name", "主機名"),
        ["space"] = ("Space", "空間"),
        ["name"] = ("Name", "名稱"),
        ["capacity"] = ("Capacity", "容量"),
        ["mode"] = ("Mode", "模式"),
        ["disk"] = ("Disk", "磁碟"),
        ["na"] = ("N/A", "N/A"),
        ["app_brand"] = ("Momo System Monitor", "Momo 系統監測"),
        // The splash sets "Momo" in the display face and this line beneath it, so the
        // descriptor is kept apart from app_brand rather than repeating the name.
        ["app_descriptor"] = ("SYSTEM MONITOR", "系統監測"),
        ["splash_loading"] = ("Reading sensors…", "正在讀取感測器…"),
        ["fans"] = ("Fans", "風扇"),
        ["fan_source"] = ("Follow", "依據"),
        ["fan_source_cpu"] = ("CPU", "CPU"),
        ["fan_source_gpu"] = ("GPU", "GPU"),
        ["fan_auto"] = ("Auto", "自動"),
        ["fan_custom"] = ("Custom", "自訂"),
        ["fan_apply"] = ("Apply", "確定"),
        ["nav_fans"] = ("Fans", "風扇"),
        ["fan_by_sensor"] = ("Based on a temperature", "依溫度調整"),
        ["fan_constant"] = ("Constant speed", "固定轉速"),
        ["fan_start"] = ("Start increasing at", "開始提速溫度"),
        ["fan_max"] = ("Full speed at", "全速溫度"),
        ["fan_mode_software"] = ("software control", "軟體控制中"),
        ["fan_mode_auto"] = ("firmware control", "韌體自動"),
        ["fans_intro"] = (
            "Every fan starts on Auto. Custom speeds stay within the range the hardware reports, and every fan is handed back to its firmware when the app exits.",
            "每顆風扇預設為「自動」。自訂轉速會被限制在硬體回報的範圍內；程式結束時一律交還韌體控制。"),
        ["fans_none"] = (
            "This machine exposes no controllable fan. OEM and laptop boards usually lock their fan controller, so no software can drive it — including dedicated fan tools, which read the same sensors.",
            "這台機器沒有可控制的風扇。OEM 與筆電主機板通常鎖住風扇控制器，任何軟體都無法驅動 —— 包括專門的風扇工具，它們讀的是同一組感測器。"),
        ["refresh_interval"] = ("Refresh interval", "更新間隔"),
        ["refresh_1"] = ("1 second", "1 秒"),
        ["refresh_2"] = ("2 seconds (recommended)", "2 秒（建議）"),
        ["refresh_5"] = ("5 seconds", "5 秒"),
        ["refresh_hint"] = (
            "Nearly all of this app's own draw; changes accumulated energy by under 0.5%. Drops to 5 s while no window is on screen.",
            "幾乎就是本程式自身的耗電來源，對累計用電的影響在 0.5% 以內。沒有視窗在畫面上時自動降到 5 秒。"),
        ["hdr_summary"] = ("OVERVIEW", "總覽"),
        ["hdr_cpu"] = ("CPU", "CPU · 處理器"),
        ["hdr_gpu"] = ("GPU", "GPU · 顯卡"),
        ["hdr_ram"] = ("MEMORY", "記憶體"),
        ["hdr_network"] = ("NETWORK", "網路"),
        ["hdr_storage"] = ("STORAGE", "儲存"),
        ["hdr_fans"] = ("FANS & PUMP", "風扇與幫浦"),
        ["hdr_top"] = ("TOP PROCESSES", "熱門處理程序"),
        ["utilization"] = ("Utilization", "使用率"),
        ["speed"] = ("Speed", "速度"),
        ["base_speed"] = ("Base speed", "基準速度"),
        ["logical_processors"] = ("Logical processors", "邏輯處理器"),
        ["l3_cache"] = ("L3 cache", "L3 快取"),
        ["dedicated_memory"] = ("Graphics memory", "顯示記憶體"),
        ["driver_version"] = ("Driver version", "驅動程式版本"),
        ["driver_date"] = ("Driver date", "驅動程式日期"),
        ["vram"] = ("VRAM", "顯存"),
    };
}



