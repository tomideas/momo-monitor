"""Build the offline English guide. Run from any directory; no network required."""
from pathlib import Path
from html import escape
import json
import shutil
import xml.etree.ElementTree as ET
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SITE = ROOT / 'site'
RELEASES = 'https://github.com/tomideas/momo-monitor/releases'
VERSION = ET.parse(ROOT / 'StatusMonitor/StatusMonitor.csproj').findtext('./PropertyGroup/Version')
PAGES = []

def figure(name, alt, caption):
    w, h = Image.open(SITE / 'assets/images' / name).size
    return f'''<figure class="doc-figure"><a class="image-link" href="assets/images/{name}" target="_blank" rel="noopener" aria-label="{escape(alt)} — open full-size image"><img src="assets/images/{name}" width="{w}" height="{h}" alt="{escape(alt)}" loading="lazy" decoding="async"></a><figcaption>{caption} <span>Open the image for a closer look.</span></figcaption></figure>'''

def steps(*items):
    return '<ol class="steps">' + ''.join(f'<li>{s}</li>' for s in items) + '</ol>'

def table(headers, rows):
    return '<div class="table-scroll" tabindex="0" role="region" aria-label="' + escape(headers[0] + ' reference table') + '"><table><thead><tr>' + ''.join(f'<th scope="col">{h}</th>' for h in headers) + '</tr></thead><tbody>' + ''.join('<tr>' + ''.join(f'<td>{c}</td>' for c in row) + '</tr>' for row in rows) + '</tbody></table></div>'

def note(text):
    return f'<aside class="callout"><p>{text}</p></aside>'

def page(slug, title, description, body, keywords=''):
    PAGES.append(dict(slug=slug, title=title, description=description, body=body, keywords=keywords))

page('index', 'Meet Momo Monitor', 'Your Windows PC, at a glance. A practical guide to readings, cooling, energy and everyday monitoring.', '''
<div class="hero-actions"><a class="button primary" href="getting-started.html">Get started</a><a class="button" href="installation.html">Install Momo</a></div>
''' + figure('paper-dashboard-en.png', 'English Momo dashboard with power, energy and CPU, GPU, RAM, disk and network readings', 'The current English Dashboard in Paper Pop. This capture has limited sensor access; unavailable temperature readings remain visible as dashes.') + '''
<h2 id="features">Find what you need</h2>
<div class="topic-grid">
<a class="topic" href="dashboard.html"><strong>Read your system</strong><span>Understand each dashboard row and investigate a reading.</span></a>
<a class="topic" href="fans.html"><strong>Manage cooling</strong><span>Inspect fan speeds, choose Auto or configure a temperature curve.</span></a>
<a class="topic" href="process.html"><strong>Find a busy app</strong><span>Follow live trends and rank processes by power, memory or disk activity.</span></a>
<a class="topic" href="power.html"><strong>Track energy &amp; cost</strong><span>Understand power scope, monitoring coverage and electricity rates.</span></a>
<a class="topic" href="mini.html"><strong>Keep Momo nearby</strong><span>Use the compact panel, system tray and Windows startup.</span></a>
<a class="topic" href="alerts.html"><strong>Investigate an alert</strong><span>Set thresholds, review incidents and follow related readings.</span></a>
</div>
<h2 id="basics">Before you begin</h2>
<p>Momo is a Windows 11 x64 desktop application. The standard portable package includes its .NET runtime. Extract the complete folder, then run <code>MomoMonitor.exe</code>. Protected hardware readings need administrator access and the supported sensor driver.</p>
<p>This guide uses the <strong>English interface</strong> in version <strong>2.22.32</strong>. Set <strong>Settings → General → Language → English</strong>, then press <strong>OK</strong>. Screenshots come from the actual WPF application; readings and available controls vary by computer. Alert examples are explicitly identified.</p>
''' + note('<strong>Read the label beside the watts.</strong> A power figure may cover components, CPU + GPU, battery discharge or estimated wall input. Those scopes answer different questions. See <a href="power.html">Power, energy &amp; cost</a>.') + '''
<h2 id="navigation">Where things live</h2>
''' + table(['In Momo', 'Use it for'], [('Dashboard', 'Live usage, power and cumulative energy.'), ('Info', 'Hardware cards, specifications and sensor details.'), ('Fans', 'Fan monitoring and supported hardware controls.'), ('Process', 'Live trend charts and process rankings.'), ('Alerts', 'Seven days of incidents, recovery and acknowledgement.'), ('Mini button', 'A compact floating panel with live readings.'), ('Settings gear', 'Appearance, language, monitoring, rates and local data.')]), 'overview introduction features guide English')

page('installation', 'Install & run', 'Extract the portable package, enable the readings you need, and launch your first session.', '''
<h2 id="requirements">What you need</h2>
<ul><li>Windows 11, 64-bit (x64).</li><li>A writable local folder for Momo and its data.</li><li>Administrator access for protected hardware sensors and optional Windows startup registration.</li></ul>
<p>The standard portable release includes .NET. Only the optional framework-dependent developer build needs the .NET 8 Desktop Runtime.</p>
<h2 id="install">Install the portable release</h2>
''' + steps(f'Open <a href="{RELEASES}" target="_blank" rel="noopener">Momo releases</a> and download the Windows x64 portable package.', 'Extract the <strong>whole ZIP</strong> to a writable local folder. Keep <code>momo-data</code> beside <code>MomoMonitor.exe</code>. Do not run the program from inside the ZIP.', 'Double-click <code>MomoMonitor.exe</code> and allow the Windows administrator prompt if you want full sensor access.', 'Wait for sensor warm-up. The startup animation plays once before the Dashboard opens.', 'Open the gear → <strong>General</strong>, set <strong>Language</strong> to <strong>English</strong>, then press <strong>OK</strong>.') + figure('settings-en.png', 'Settings General tab with English selected in Language and OK at the bottom', 'Choose English in General, then save with OK.') + '''
<h2 id="driver">Enable full hardware sensing</h2>
<p>CPU temperature, CPU package power and motherboard fan speeds may need the signed PawnIO driver. Momo carries its installer and offers it when the machine needs it.</p>
''' + steps('When Momo asks <strong>Turn on full hardware sensing?</strong>, select <strong>Install and reopen Momo</strong>.', 'Let the installation finish. Windows does not need to restart; Momo reopens itself.', 'If you chose <strong>Not now</strong>, open <strong>Settings → Monitoring &amp; alerts → Full hardware sensing</strong> to try again later.') + figure('monitoring-en.png', 'English Monitoring and alerts settings with primary GPU and sensor settings', 'Monitoring & alerts is also where you configure the primary GPU and thresholds. The sensing offer is conditional on the machine.') + note('Installing the driver improves access; it does not guarantee that every motherboard or GPU exposes every sensor. See <a href="troubleshooting.html#missing">Missing readings</a>.') + '''
<h2 id="update">Update an existing copy</h2>
''' + steps('Exit Momo from its tray menu before replacing files.', 'Back up the complete existing folder, including <code>momo-data</code>.', 'Extract the new package. Keep your existing data together with the new executable; do not replace populated data with an empty folder.', 'Run the new version and check the version in Settings, the data location, and hardware preferences.') + '''
<h2 id="uninstall">Remove Momo</h2>
<p>First disable <strong>Settings → General → Start with Windows</strong> and press <strong>OK</strong>. Exit Momo, then remove the application folder. Save a copy of <code>momo-data</code> if you want to retain history. The optional sensor driver is installed on the PC separately from the portable folder.</p>''', 'download zip portable install driver PawnIO setup runtime update uninstall UAC')

page('getting-started', 'Quick start', 'A visual walkthrough from your first launch to a useful everyday monitoring setup.', '''
<h2 id="walkthrough">Follow the English interface</h2>
<p>Use the next and previous buttons to work through the screenshots at your own pace. Each step names the control and the expected result.</p>
<div class="walkthrough" aria-label="Quick start walkthrough">
<div class="walkthrough-controls" hidden><button type="button" data-prev>Previous step</button><span data-step-status role="status" aria-live="polite"></span><button type="button" data-next>Next step</button></div>
<section class="walkthrough-step"><h3>1. Choose English and your appearance</h3><p>Open the gear at the top right. In <strong>General</strong>, choose <strong>Language → English</strong> and <strong>Background → Paper Pop (light)</strong> or <strong>Volt (dark)</strong>. Press <strong>OK</strong> to save. Cancel discards the draft.</p>''' + figure('settings-en.png', 'General settings showing English and Paper Pop light', 'The gear opens Settings; General owns language and appearance.') + '''</section>
<section class="walkthrough-step"><h3>2. Check the Dashboard</h3><p>Read the wattage <em>label</em> first, then the CPU, GPU and RAM percentages. The DISK bars show used capacity. A sensor notice explains limited readings; a dash means unavailable.</p>''' + figure('paper-dashboard-en.png', 'Dashboard with English labels and CPU GPU RAM DISK NET rows', 'The CPU + GPU label in this capture describes the available scope.') + '''</section>
<section class="walkthrough-step"><h3>3. Inspect a busy process</h3><p>Select <strong>RAM</strong> on the Dashboard to open memory trends and memory-ranked processes, or open <strong>Process</strong> directly. Select a metric and a 60-second or 15-minute range. Wait for samples to arrive.</p>''' + figure('process-en.png', 'Process page with trend chart controls and the process ranking table', 'Process brings trends and process rankings together.') + '''</section>
<section class="walkthrough-step"><h3>4. Review alert thresholds</h3><p>Open <strong>Settings → Monitoring &amp; alerts</strong>. Choose your primary GPU and thresholds, then press <strong>OK</strong>. To inspect incidents, open the separate <strong>Alerts</strong> page.</p>''' + figure('monitoring-en.png', 'Monitoring and alerts settings for the primary GPU and threshold options', 'Review thresholds before relying on notifications.') + '''</section>
<section class="walkthrough-step"><h3>5. Switch to the Mini panel</h3><p>Use the <strong>Mini</strong> button beside the gear. Drag the panel to a convenient position. Double-click its background or select <strong>Open Dashboard</strong> to return. Set always-on-top behavior in General.</p>''' + figure('mini-en.png', 'English Mini panel with power and compact hardware readings', 'The compact panel keeps live readings beside your work.') + '''</section></div>
<h2 id="next">Finish your setup</h2>
''' + steps('In General, select your <strong>Country / Region</strong>. Check the electricity price, currency and carbon intensity; use your own rates if needed.', 'Keep <strong>Fans → Auto</strong> until you have confirmed which hardware each fan belongs to.', 'Enable <strong>Keep running in the tray when closed or minimized</strong> if you want background monitoring.', 'Open <strong>Settings → Data &amp; Info</strong> and check for storage warnings.') + note('The default visible refresh interval is <strong>2 seconds</strong>. When all windows are hidden or minimized, Momo uses a slower background interval. This is normal; see <a href="settings.html#refresh">Refresh settings</a>.'), 'tutorial first launch walkthrough English language quick start')

page('dashboard', 'Dashboard', 'Read your hardware at a glance, then select a row to investigate it.', figure('paper-dashboard-en.png', 'English Dashboard with live hardware usage rows and energy period buttons', 'Paper Pop Dashboard. Hardware availability and values depend on your PC.') + '''
<h2 id="readings">What each reading means</h2>
''' + table(['Reading', 'Meaning', 'What to check'], [ ('Power', 'Current watts for the scope named above the number.', 'The scope label and missing-sensor notices; <a href="power.html">read about power</a>.'), ('CPU', 'Processor usage, frequency, available package power and temperature.', 'Select the CPU row to inspect temperature trends.'), ('GPU', 'One row per visible GPU: load, clocks, memory, temperatures and available power.', 'The GPU model and named sensor. Integrated memory can be shared.'), ('RAM', 'Used and total system memory, plus available memory.', 'Select RAM to find memory consumers.'), ('DISK', 'Used capacity and used percentage for each fixed drive.', 'A full bar means a full drive, not busy disk I/O.'), ('NET', 'Current download and upload rates.', 'Direction and units, such as B/s, KB/s or MB/s.')]) + '''
<h2 id="inspect">Investigate a reading</h2>
''' + steps('Select <strong>CPU</strong> or a <strong>GPU</strong> row for its temperature trends.', 'Select <strong>RAM</strong> for memory trends and memory-ranked processes.', 'Select <strong>DISK</strong> for processes ranked by disk activity.', 'Use the metric controls on <strong>Process</strong> to change the view. Inspecting another GPU does not change your primary GPU for alerts.') + '''
<h2 id="periods">Choose an energy period</h2>
<p>Select <strong>Today</strong>, <strong>7 days</strong>, <strong>30 days</strong> or <strong>All</strong> above the cumulative figures. Energy, Carbon and Cost use the selected period; current watts remain a live reading. Totals cover recorded monitoring, not every hour the PC has existed.</p>
<h2 id="states">Understand colors and missing data</h2>
<p>The accent color identifies ordinary readings. The alarm color marks readings over their configured limits. Read the numeric value and unit as well as the color. A <code>—</code> means no valid reading is available. A reported <strong>0</strong> remains a valid zero.</p>
<p>Readings delayed or hardware-sensing notices explain stale samples and limited access. Use <strong>Read again</strong> or the monitoring action when offered. Missing samples are not filled with zero.</p>
<h2 id="skins">Paper Pop and Volt</h2>
<p>Open <strong>Settings → General → Background</strong>. Choose Paper Pop (light) or Volt (dark), then press OK. Both use the same layout.</p>
''' + figure('volt-dashboard-en.png', 'English Dashboard in the dark Volt skin with lime accent readings', 'Volt uses the same readings and controls with a dark palette.'), 'cpu gpu ram memory disk storage network dashboard skins paper volt')

page('power', 'Power, energy & cost', 'Understand the measurement scope before comparing wattage, battery use or cumulative totals.', '''
<h2 id="scope">Start with the power label</h2>
''' + table(['Power scope', 'What it includes', 'Limit'], [('Estimated power', 'Available component sensors and supported estimates.', 'Coverage depends on hardware; this is not a wall-meter measurement.'), ('CPU + GPU power', 'Available CPU/GPU telemetry or explicitly supplied GPU calibration.', 'Excludes the display, peripherals and adapter losses; used on notebooks and unknown chassis where desktop curves are inappropriate.'), ('Battery discharge', 'Valid Windows battery-side discharge power.', 'Excludes adapter losses; requires supported absolute units and compatible battery states.'), ('Estimated power at the wall', 'A supported desktop estimate including the configured power-supply losses.', 'Depends on PSU information and estimation; a physical wall meter is needed to measure AC input.')]) + note('<strong>Charging watts are not computer consumption.</strong> Battery charging rate is shown separately. CPU package power already includes integrated-GPU power where applicable; Momo does not add that domain again.') + '''
<h2 id="energy">Energy, carbon and electricity cost</h2>
<p>Power (W) is the current rate of consumption. Energy (Wh or kWh) accumulates that rate over valid monitoring time. At 100 W for one monitored hour, energy is 100 Wh, or 0.1 kWh. At $0.20/kWh, that interval costs $0.02; at 400 g/kWh, it represents 40 g of carbon emissions.</p>
<p>Sleep, suspend/resume and long sampling gaps break integration. Momo does not fill sleeping hours with the last observed wattage. Read the coverage note for recorded monitoring time, missing-power time and mixed scopes. Older records may have no coverage metadata.</p>
<h2 id="rates">Set your rates</h2>
''' + steps('Open <strong>Settings → General</strong>.', 'Choose <strong>Country / Region</strong> to fill electricity price, currency and carbon intensity together.', 'Adjust <strong>Electricity price</strong> and <strong>Carbon intensity</strong> to your own assumptions. Manual adjustments select Custom.', 'Press <strong>OK</strong>. Read the period label when comparing Energy, Carbon and Cost.') + figure('settings-en.png', 'General settings with country region electricity price currency and carbon intensity fields', 'Energy & rates contains the assumptions used for cost and carbon.') + '''
<h2 id="wall">Configure a supported desktop PSU estimate</h2>
<p>In <strong>Settings → Monitoring &amp; alerts</strong>, scroll to <strong>Whole-machine power at the wall</strong>. Enable <strong>Count the power supply’s losses</strong>, then enter the PSU rated watts and its efficiency class from the supply label. Press OK. This is optional and off by default; do not apply a desktop PSU assumption to notebook adapter power.</p>
<h2 id="gpu">An unrecognised GPU</h2>
<p>If a GPU cannot report power or its power limit, a board-power field may appear in Settings. Enter that specific card’s documented board power or leave it empty. The field accepts 1–1000 W. Calibration is an estimate, not new sensor telemetry.</p>
<h2 id="process-power">Per-process watts</h2>
<p>Process power is attributed from CPU/GPU activity; it is not separately measured at each application. Use it to compare active workloads within a session. Process attribution and totals should be interpreted with the current machine-power scope.</p>''', 'power watt watts energy Wh kWh battery charging carbon cost PSU wall notebook')

page('fans', 'Fans & cooling', 'Inspect the cooling hardware your PC exposes and configure supported fan controls.', '''
<h2 id="inspect">Check your fans first</h2>
''' + steps('Open <strong>Fans</strong> in the top navigation.', 'Read each fan’s hardware name, temperature and temperature source, RPM and control status.', 'Enable <strong>Show all fans</strong> to inspect hidden idle channels.', 'Leave the fan on <strong>Auto</strong> while identifying it. Rename a generic header when you know what it is connected to.') + figure('fans-en.png', 'English Fans page showing the hardware-control state on this machine', 'Actual Fans page on the capture machine. Protected or unsupported controls may be unavailable; this is not evidence that a custom command was applied.') + '''
<h2 id="modes">Auto and Custom</h2>
''' + table(['Mode', 'Behavior'], [('Auto', 'The hardware firmware owns fan speed. This is the default.'), ('Custom: temperature curve', 'Speed ramps from the fan’s minimum to maximum between your start and maximum temperatures.'), ('Custom: constant speed', 'A fixed percentage within the range supported by that fan.')]) + '''
<h2 id="curve">Configure a temperature curve</h2>
''' + steps('On a supported controllable fan, choose <strong>Custom</strong>. The editor opens beneath that fan.', 'Choose <strong>Based on a temperature</strong>. Set <strong>Start increasing at</strong> and <strong>Full speed at</strong> for your cooling setup, or choose <strong>Constant speed</strong>.', 'Review the fields and press <strong>Apply</strong>. Changes in the editor do not control hardware until applied.', 'Check the status: <strong>Custom control applied</strong> confirms the command took effect. Observe the temperature and RPM.', 'Choose <strong>Auto</strong> to return speed control to firmware.') + figure('fan-custom-en.png', 'English fan Custom editor showing Based on a temperature Start increasing at Full speed at Constant speed and Apply', 'The actual Custom editor in read-only preview. Monitoring only means this capture did not write a fan command.') + '''
<p>A GPU fan follows its own GPU temperature. A motherboard fan follows CPU temperature. There is no Follow selector or cross-category fallback when that temperature is missing. Constant mode uses a fixed speed instead of a temperature ramp.</p>
<h2 id="status">Understand the status</h2>
''' + table(['Status', 'Meaning / action'], [('Firmware control', 'Auto is active and firmware owns speed.'), ('Waiting to apply', 'The command has not yet taken effect.'), ('Custom control applied', 'The custom command was accepted.'), ('Temperature unavailable; firmware control', 'The required temperature is missing; control returns to firmware.'), ('Driver refused custom control; firmware control', 'The hardware/driver did not accept the command. Use Auto and check support.'), ('Could not restore firmware control; retrying', 'The release failed and Momo is retrying; check the cooling state.'), ('Monitoring only', 'Readings are available, but custom hardware control is not active.'), ('Stopped (0 RPM)', 'A valid zero RPM; it may be a device stop mode.'), ('RPM unavailable', 'No valid RPM reading; this is different from a stopped fan.')]) + '''
<h2 id="portable-fans">Moving to another computer</h2>
<p>Portable histories and general preferences travel, but fan profiles return to firmware Auto on another PC. Hardware-specific labels and calibration must be checked again. On a normal application exit, Momo releases software fan control.</p>''', 'fans pump cooling RPM Auto Custom constant curve temperature firmware control')

page('process', 'Processes & trends', 'Follow a metric over time and find the applications contributing to the workload.', figure('process-en.png', 'English Process page with trend metric selector range buttons and a process list', 'The Process page contains both trend charts and process rankings. A new session begins with a short history.') + '''
<h2 id="trends">Read a trend</h2>
''' + steps('Open <strong>Process</strong> from the top navigation, or select CPU, GPU, RAM or DISK on the Dashboard.', 'Choose the metric: CPU/GPU load, RAM, GPU memory, available temperature or power.', 'Choose <strong>60 s</strong> for recent changes or <strong>15 min</strong> for a longer view.', 'Move the pointer over the chart for sample time and value. Read the displayed minimum, average and maximum.', 'For GPU metrics, check the selected GPU. Inspecting it leaves the primary-GPU alert preference intact.') + '''
<p>Live trend history covers this launch only, up to the last 15 minutes. It is separate from persistent daily energy and alert history. A restart starts a new chart. Missing sensors and interrupted samples leave gaps; they are not zero-valued samples.</p>
<h2 id="ranking">Rank processes</h2>
<p>Use <strong>Rank processes by</strong> to compare attributed power, memory or disk activity. RAM inspection selects memory ranking; DISK inspection selects disk activity. The number of displayed processes is controlled by <strong>Settings → General → Processes shown</strong>.</p>
<p>Per-process power is attributed using CPU/GPU activity, not a physical measurement. A high memory ranking is not automatically a fault; compare it with available RAM and the current workload. Momo’s process view is for inspection; use Windows tools to manage an application you decide to close.</p>
<h2 id="empty">A chart or table looks empty</h2>
<p>Wait for the next successful samples. Check the chosen metric, GPU and sensor notice. An unavailable temperature produces no temperature curve. See <a href="troubleshooting.html#missing">Missing readings</a> if the metric stays unavailable.</p>''', 'process processes trend trends chart history memory disk ranking CPU GPU')

page('mini', 'Mini panel & system tray', 'Keep the readings beside your work and decide how Momo behaves in the background.', figure('mini-en.png', 'Compact English Mini panel with power CPU GPU RAM disk and network values', 'The current Mini panel. Each drive keeps its own row; many drives scroll within the disk group.') + '''
<h2 id="mini">Open and position the Mini panel</h2>
''' + steps('Select the <strong>Mini</strong> button beside the Settings gear.', 'Drag the panel background to move it. Buttons and disk scrollbars retain their own actions.', 'Read percentages as the main values and clocks or used memory/capacity beneath them. The disk rows stay in volume order.', 'Double-click the background or select <strong>Open Dashboard</strong> to return.') + '''
<p>In <strong>Settings → General</strong>, enable <strong>Keep the mini panel on top of other windows</strong> if desired. Enable <strong>Start in mini mode</strong> for manual launches. The panel position is remembered.</p>
<h2 id="mascot">Momo’s animation</h2>
<p>Showing Mini or moving the pointer over an idle panel plays the hover animation once. Dragging plays its separate motion once. Playback returns to a resting pose. <strong>Reduce motion</strong> keeps the mascot static.</p>
<h2 id="tray">Keep monitoring in the tray</h2>
<p>In <strong>Settings → General</strong>, enable <strong>Keep running in the tray when closed or minimized</strong>, then press OK. Hiding the window continues sampling and alerts. Double-click the system-tray icon to open Momo; right-click it for the tray menu. Use <strong>Exit</strong> to stop monitoring and save totals.</p>
<p>By default the close action asks whether to exit or keep running. If you choose not to be asked again, the saved tray behavior controls future closes. Ordinary minimize behavior is retained when tray running is disabled.</p>
<h2 id="startup">Start with Windows</h2>
''' + steps('Run Momo as administrator.', 'Enable <strong>Settings → General → Start with Windows</strong> and press <strong>OK</strong>.', 'At the next sign-in, Momo starts in the tray after about 20 seconds, without opening a panel.', 'Double-click the tray icon to open the Dashboard. Disable Start with Windows before deleting this portable copy.') + figure('runtime-settings-en.png', 'English General settings showing refresh interval startup mini and tray preferences', 'Scroll down General for Windows startup, Mini mode and tray behavior.') + note('Windows sign-in uses a scheduled task with administrator privileges. It opens in the tray even if manual launches are set to Mini. Driver installation and the startup task belong to the current PC.'), 'mini widget floating always on top tray startup Windows logon close exit mascot')

page('alerts', 'Alerts & incident history', 'Set sustained thresholds, then follow an event from its first breach to recovery.', '''
<h2 id="configure">Configure alerts</h2>
''' + steps('Open the gear → <strong>Monitoring &amp; alerts</strong>.', 'Enable <strong>Notify on sustained threshold breaches</strong>. Choose the <strong>Primary GPU</strong> for GPU alerts.', 'Review the temperature, memory and disk thresholds, sustained duration and notification cooldown.', 'Press <strong>OK</strong> to commit the dialog. Cancel discards its draft.') + figure('alert-settings-en.png', 'English alert threshold settings with CPU GPU memory disk limits and notification timing', 'Scroll within Monitoring & alerts to reach these threshold and timing fields.') + table(['Default rule', 'Trigger'], [('CPU temperature', 'Above 90 °C.'), ('Primary GPU temperature', 'Above 85 °C.'), ('RAM / GPU memory', 'Above 90% used.'), ('Fixed disk', 'Below 10% free space.'), ('Sustained duration', '15 seconds before the condition fires.'), ('Notification cooldown', '300 seconds between eligible repeat notifications.')]) + '''
<p>Missing readings never trigger an alert. A short spike may end before the sustained duration expires. GPU alerts follow your primary-GPU preference; inspecting another GPU does not change it.</p>
<h2 id="investigate">Follow up an incident</h2>
''' + steps('Open <strong>Alerts</strong>, or select a Momo notification to open its event.', 'Select an incident to read its threshold, start/end time, observed duration and peak or lowest value.', 'Select <strong>View related readings</strong> to investigate the corresponding trend or hardware information.', 'Select <strong>Acknowledge</strong> when reviewed. Acknowledgement records that you saw it; it does not repair the condition or erase its history.') + figure('alerts-en.png', 'English Alerts page showing an active CPU event and recovered memory incident with event details', 'Example events supplied by the application’s read-only preview: an active CPU-temperature incident and a recovered memory incident. These are not real incidents on the capture machine.') + '''
<h2 id="states">Event states</h2>
<ul><li><strong>Active:</strong> the sustained condition is still observed.</li><li><strong>Recovered:</strong> a later valid reading returned within the limit.</li><li><strong>Interrupted:</strong> observation ended or monitoring was interrupted; this is not a confirmed recovery.</li></ul>
<p>The last seven days of events persist across restarts. Restart gaps are excluded from observed incident duration. Windows Do Not Disturb can suppress notifications; the in-app history remains the place to review events.</p>''', 'alerts notifications incident threshold cooldown sustained recovery acknowledge')

page('info', 'Hardware & sensor details', 'Check what is installed, copy specifications, and compare named sensor sources.', '''
<h2 id="hardware">Open hardware information</h2>
''' + steps('Open <strong>Info</strong> in the top navigation.', 'Read the hardware and operating-system cards. Fixed drives share a card with one capacity row per drive.', 'Select <strong>View details</strong> on a card to open its full specifications.', 'Select <strong>Copy specifications</strong> to copy the text. Press <kbd>Esc</kbd> to close details.', 'Select <strong>Refresh</strong> after hardware or system information changes.') + figure('info-en.png', 'English Info page with hardware cards and Refresh control', 'Info begins with Refresh and hardware cards. Specifications are refreshed separately from live sampling.') + '''
<h2 id="sensors">Inspect sensor sources</h2>
<p>Expand <strong>Sensor details</strong> for a device where available. Check the sensor name, unit and measurement scope before comparing another tool. CPU package temperature, GPU core, hot spot and memory-junction temperature are distinct readings; unsupported values stay unavailable.</p>
<p>Minimum and maximum values cover the current monitoring session. A sensor marked missing or invalid should not be compared with a measured zero.</p>
<h2 id="export">Export a sensor report</h2>
''' + steps('Choose <strong>Export sensor report</strong> when sensor details are available.', 'Choose a writable local folder and save the UTF-8 CSV.', 'Compare the same named sensors, units and scopes at a similar sample time in your other hardware-monitoring tool.') + '''
<p>The report includes sensor names and IDs, scopes, current/minimum/maximum values, reading states, sample time and the bundled library fingerprint. It collects no host name or hardware serial numbers. Other specification text can contain identifying hardware details; review copied information before sharing it.</p>''', 'info hardware specifications WMI sensors report export CSV temperature source')

page('settings', 'Settings', 'Choose appearance, rates and monitoring preferences, then save them as one change.', '''
<h2 id="save">Open, edit and save</h2>
<p>Select the gear at the top right. The dialog has <strong>General</strong>, <strong>Monitoring &amp; alerts</strong> and <strong>Data &amp; Info</strong> tabs. Scroll inside a tab to reach the remaining fields.</p>
<p><strong>OK</strong> validates and commits the draft; <strong>Cancel</strong>, the close button or <kbd>Esc</kbd> discards it. Appearance changes may preview while the dialog is open and revert on Cancel. Correct any inline validation message before saving.</p>
''' + figure('settings-en.png', 'Settings dialog General tab with OK and Cancel buttons', 'General settings in English; the footer shows version 2.22.32.') + '''
<h2 id="general">General</h2>
''' + table(['Option', 'What it changes'], [('Background', 'Paper Pop (light) or Volt (dark).'), ('Language / Font', 'English or Chinese and the supported font choices.'), ('Reduce motion', 'Keeps the startup and Mini mascot static and reduces UI motion.'), ('Country / Region', 'Electricity price, currency and carbon intensity together.'), ('Electricity price / Carbon intensity', 'Your assumptions per kWh; <a href="power.html#rates">rate setup</a>.'), ('Processes shown', 'The process list length.'), ('Refresh interval', 'Visible sensor sampling rate.'), ('Run in the system tray', 'Keeps sampling and alerts active while hidden.'), ('Start with Windows', 'Registers the administrator logon task.'), ('Mini preferences', 'Always-on-top behavior and manual-launch Mini mode.')]) + '''
<h2 id="refresh">Refresh interval</h2>
<p>The visible interval defaults to <strong>2 seconds (recommended)</strong>; options include 1, 2 and 5 seconds. Faster sampling reacts sooner but performs more sensor queries. When all windows are hidden or minimized, Momo uses the slower idle interval (5 seconds by default).</p>
<h2 id="monitoring">Monitoring & alerts</h2>
<p>Choose your primary GPU, optionally hide the integrated GPU when a dedicated GPU exists, configure PSU-loss estimates and alert limits, and enable missing-driver installation when offered. Automatic GPU selection prefers a dedicated card. A missing saved GPU falls back to automatic.</p>
''' + figure('monitoring-en.png', 'Monitoring and alerts settings with GPU options', 'GPU preferences and thresholds share Monitoring & alerts.') + '''
<h2 id="data">Data & Info</h2>
<p>Read the local-data guidance and any storage warnings before making a backup. Settings and histories are local. Find <code>momo-data</code> beside the executable, or the compatibility AppData folder described in <a href="portable.html">Portable data &amp; backups</a>.</p>
''' + figure('data-en.png', 'Data and Info tab with local data information and reset totals control', 'Data & Info shows the storage information and cumulative reset action.') + '''
<h2 id="reset">Reset cumulative totals</h2>
<p><strong>Reset totals</strong> clears recorded energy, carbon and cost while keeping preferences. Back up your data first if you want to retain the records. Confirm the action twice inside the dialog, then press <strong>OK</strong> to execute the pending reset. <strong>Cancel</strong> keeps the totals.</p>''', 'settings appearance language font refresh rates save cancel data reset GPU')

page('portable', 'Portable data & backups', 'Keep your preferences and history together, and move the complete folder safely.', r'''
<h2 id="location">Where data lives</h2>
<p>With <code>momo-data</code> beside the executable, Momo stores local data there. Check <strong>Settings → Data &amp; Info</strong> for storage warnings. Without that folder, the compatibility location is <code>%APPDATA%\StatusMonitor\</code>.</p>
''' + table(['File', 'Contents'], [('settings.json', 'Preferences, hardware configuration and a one-way local machine marker.'), ('totals.json', 'Cumulative energy, retained across restarts.'), ('energy-history.json', 'Daily energy records, approximately 400 days.'), ('alert-history.json', 'Recent incidents and recovery information, covering seven days.'), ('*.bak', 'The previous valid version retained by atomic saves.')]) + figure('data-en.png', 'Data and Info showing local-data guidance and cumulative reset control', 'Data & Info explains local storage and shows storage warnings when there is a problem.') + '''
<h2 id="backup">Back up or move Momo</h2>
''' + steps('Exit Momo from the tray menu to save the current totals.', 'Copy the <strong>complete application folder</strong>, including the executable and populated <code>momo-data</code>, to your backup or destination.', 'Run Momo from the copied folder, confirm your histories are present and check Data &amp; Info for warnings.', 'On a different PC, recheck hardware preferences and optional driver/startup setup. Fan controls return to Auto.') + '''
<p>When moved to another PC, general preferences and histories remain. Fan identities and profiles, GPU selection, power calibration, Windows startup and Mini-start preferences, and window positions are reset for the new machine. Confirm them before enabling custom hardware control.</p>
<h2 id="migration">Legacy AppData import</h2>
<p>An empty portable data folder imports an existing AppData set once. Existing portable data prevents merging. The original AppData files remain intact. Removing <code>momo-data</code> deliberately switches to the compatibility location; it is not a backup procedure.</p>
<h2 id="recovery">Storage problems and recovery</h2>
<p>Saves are atomic and keep the previous file as <code>.bak</code>. If a file is damaged, Momo tries its backup, reports recovery and preserves the damaged original. If no valid copy exists, the damaged original is protected from being overwritten by default values.</p>
<p>For a write failure, check free space and folder permissions. Move the whole portable folder somewhere writable and restart. Momo reports the failure and does not silently redirect read-only portable data to AppData.</p>
''' + note('The PawnIO driver and Windows logon task are installed on the current PC. They do not travel with the portable data. Disable Start with Windows before deleting an enabled copy.'), 'portable data folder backup restore history JSON storage recovery AppData migrate')

page('troubleshooting', 'Troubleshooting & FAQ', 'Find the symptom, check the cause, and follow the recovery steps.', '''
<h2 id="missing">Some values show a dash</h2>
<p><code>—</code> means unavailable, not zero. Start with the hardware-sensing notice on the Dashboard.</p>
''' + steps('Run Momo as administrator to access protected sensors.', 'Open <strong>Settings → Monitoring &amp; alerts</strong> and enable <strong>Full hardware sensing</strong> if the driver offer is present.', 'Let Momo reopen after installation, then use <strong>Read again</strong> when offered.', 'If only one sensor is absent, check whether that specific device exposes it. Some integrated GPUs have no fan RPM; unsupported motherboard chips may expose no board fans.') + '''
<h2 id="fans">The Fans page has no controls</h2>
<p>Enable Show all fans to reveal hidden idle channels. A driver can expose monitoring without exposing writable control. If full sensing is already enabled, the board or firmware may not support LibreHardwareMonitor’s controls. Auto remains the correct option when custom commands are refused.</p>
<h2 id="power">Watts differ from another program or a wall meter</h2>
<p>Compare the same scope and sample time. CPU package, GPU board, GPU chip, battery-side discharge and wall input are different measurements. Notebook CPU + GPU power excludes other components. Estimates can depend on calibration; see <a href="power.html">Power, energy &amp; cost</a>.</p>
<h2 id="chart">A trend is empty or disappears after restart</h2>
<p>Choose a supported metric and wait for samples. Live charts cover only this launch and up to 15 minutes; restarting clears that live buffer. Missing readings leave gaps. Daily energy and seven-day alert histories are stored separately.</p>
<h2 id="notifications">No notification appears</h2>
<p>Check that alerts are enabled, the correct primary GPU is selected, and the condition lasted beyond the sustained duration. The cooldown limits repeats. Windows Do Not Disturb can suppress notifications. Open Alerts to inspect recorded events.</p>
<h2 id="startup">Start with Windows fails</h2>
<p>Run Momo as administrator, then save the preference again. It uses a scheduled logon task, not the Startup folder. At sign-in it runs in the tray after about 20 seconds. Double-click the tray icon to open the Dashboard.</p>
<h2 id="tray">Momo disappeared after closing</h2>
<p>Check the system tray, including its hidden icons. If Run in the system tray is enabled, the window can hide while monitoring continues. Double-click the icon to restore it; right-click and select Exit to stop it.</p>
<h2 id="saving">Settings or history do not save</h2>
<p>Open Data &amp; Info and read any storage warning. Extract the whole package into a writable location, check available disk space and restart. Do not run inside a ZIP. Keep damaged files and backups until recovery is confirmed.</p>
<h2 id="report">Collect a diagnostic report</h2>
<p>First use <strong>Info → Sensor details → Export sensor report</strong> when available. For command-line tools, see <a href="tools.html">Diagnostics &amp; command line</a>. When reporting an issue, include the version, Windows version, affected hardware model, symptom and sensor state. Review reports before sharing them.</p>''', 'FAQ troubleshooting missing dash zero no sensors driver fan notification startup save error')

page('tools', 'Diagnostics & command line', 'Export a sample or sensor listing without opening the everyday monitoring interface.', r'''
<h2 id="report">Start with the in-app sensor report</h2>
<p>Open <strong>Sensor details</strong> for the relevant device and choose <strong>Export sensor report</strong>. The UTF-8 CSV includes sensor identities, units, scopes, validity states and session min/max. This is the easiest route for comparing named readings.</p>
<h2 id="commands">Command-line tools</h2>
<p>Open PowerShell in the application folder. Use a writable output location. Protected sensor access still depends on privileges and driver support.</p>
<div class="code-block"><pre><code>.\MomoMonitor.exe --dump --out snapshot.json
.\MomoMonitor.exe --diag diag.txt
.\MomoMonitor.exe --render dashboard.png --english --theme paper</code></pre><button type="button" class="copy-code">Copy commands</button><span class="copy-status" role="status" aria-live="polite"></span></div>
''' + table(['Command', 'Result'], [('--dump --out snapshot.json', 'One current sample as JSON.'), ('--diag diag.txt', 'A list of visible sensors; inspect Motherboard entries when investigating board fans.'), ('--render dashboard.png --english --theme paper', 'A read-only PNG preview with English labels and Paper Pop.'), ('--render settings.png --english --settings', 'A read-only preview of Settings.'), ('--render process.png --english --process', 'A read-only preview of the Process page.'), ('--render mini.png --english --mini', 'A read-only preview of the Mini panel.')]) + '''
<p>Render previews use temporary settings, disable hardware fan writes and do not save the session into your normal histories. Alert render examples use supplied preview events. A PNG export captures a state; it does not record a video.</p>
<h2 id="source">Build from source</h2>
<p>Developers need the .NET 8 SDK. From the project root, run:</p>
<div class="code-block"><pre><code>powershell -ExecutionPolicy Bypass -File .\build.ps1
# Explicitly create a portable distribution:
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Portable</code></pre><button type="button" class="copy-code">Copy commands</button><span class="copy-status" role="status" aria-live="polite"></span></div>
<p>The normal build updates the root executable. Only <code>-Portable</code> creates a distribution folder and ZIP. Existing user settings and histories are not packaged. A framework-dependent build is an optional developer choice. See the repository README for build details.</p>''', 'diagnostics CLI command line export CSV JSON dump diag render build developer')

page('privacy', 'Local data & privacy', 'Understand what Momo stores, what the optional installations change, and what a report contains.', r'''
<h2 id="local">Local monitoring and storage</h2>
<p>Settings, energy records and alert history are stored locally in the active data folder. Portable mode keeps them in <code>momo-data</code>; the compatibility path is <code>%APPDATA%\StatusMonitor\</code>. Momo does not require an account for these monitoring features.</p>
<p>The settings include a one-way local machine marker. It lets Momo detect a copied data folder and reset hardware-specific controls before using it on another computer.</p>
<h2 id="reports">Before sharing a report</h2>
<p>The exported sensor CSV includes sensor names/IDs, units, scopes, values, validity states, sample time and a library fingerprint. The sensor CSV collects no host name or hardware serial numbers. Copied full specifications, diagnostic text and screenshots can contain other identifying information; inspect them before posting them.</p>
<h2 id="pc">Changes on the current computer</h2>
<p>Optional PawnIO installation adds a signed kernel driver for protected sensing. Start with Windows registers an administrator Task Scheduler logon task. These are separate from portable data and remain specific to that PC. Disable the startup preference before removing an enabled portable copy.</p>
<h2 id="license">Project and licences</h2>
<p>Momo Monitor is released under GPL-3.0. Sensor access uses LibreHardwareMonitor and its bundled dependencies. The signed PawnIO installer is distributed with its own licence. See the repository and the licence files included with the portable package.</p>
<p><a href="https://github.com/tomideas/momo-monitor" target="_blank" rel="noopener">Source repository</a> · <a href="https://github.com/tomideas/momo-monitor/issues" target="_blank" rel="noopener">Report an issue</a> · <a href="https://tomideas.com/" target="_blank" rel="noopener">Designed by Tom Tam</a></p>''', 'privacy local data license permissions machine marker reports')

GROUPS = [('Getting started', ['index', 'installation', 'getting-started']), ('Using Momo', ['dashboard', 'power', 'fans', 'process', 'mini', 'alerts', 'info']), ('Settings & support', ['settings', 'portable', 'troubleshooting', 'tools', 'privacy'])]

def build():
    by_slug = {p['slug']: p for p in PAGES}
    for i, p in enumerate(PAGES):
        nav = ''
        for group, slugs in GROUPS:
            nav += f'<div class="nav-group"><h2>{group}</h2>'
            for slug in slugs:
                target = by_slug[slug]
                current = ' aria-current="page" class="active"' if slug == p['slug'] else ''
                nav += f'<a href="{slug}.html"{current} data-keywords="{escape(target["keywords"])}">{target["title"]}</a>'
            nav += '</div>'
        adjacent = '<nav class="page-nav" aria-label="Guide pages">'
        adjacent += (f'<a href="{PAGES[i-1]["slug"]}.html"><span>Previous</span><strong>{PAGES[i-1]["title"]}</strong></a>' if i else '<span></span>')
        adjacent += (f'<a href="{PAGES[i+1]["slug"]}.html"><span>Next</span><strong>{PAGES[i+1]["title"]}</strong></a>' if i+1 < len(PAGES) else '<a href="index.html"><span>Back to</span><strong>Guide introduction</strong></a>')
        adjacent += '</nav>'
        html = f'''<!DOCTYPE html>
<!-- Generated by dev/site/build-guide.py; edit its content and rebuild. -->
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="description" content="{escape(p['description'])}"><meta name="color-scheme" content="light"><title>{p['title']} — Momo Monitor Guide</title><link rel="icon" href="assets/images/momo.png"><link rel="stylesheet" href="guide-tokens.css"><link rel="stylesheet" href="guide.css"><script src="guide.js" defer></script></head>
<body><a class="skip-link" href="#main">Skip to content</a>
<header class="site-header"><a class="logo" href="index.html"><img src="assets/images/momo.png" width="30" height="30" alt=""><span>Momo Monitor <span class="logo-docs">Guide</span></span></a><nav aria-label="Header"><a href="getting-started.html">Quick start</a><a href="{RELEASES}" target="_blank" rel="noopener" class="release-link">Releases <span class="sr-only">(opens a new tab)</span></a><button class="menu-toggle" type="button" aria-label="Open guide navigation" aria-controls="guide-nav" aria-expanded="false">Menu</button></nav></header>
<div class="backdrop" hidden></div><div class="page-wrapper"><aside class="sidebar" id="guide-nav" aria-label="Guide navigation"><div class="sidebar-heading">User guide <span>2.22.32</span></div><div class="sidebar-search"><label class="sr-only" for="guide-search">Search guide topics</label><input id="guide-search" type="search" placeholder="Find a topic…" autocomplete="off" aria-describedby="search-status"><button class="search-clear" type="button" aria-label="Clear search" hidden>×</button></div><p id="search-status" class="search-status" role="status" aria-live="polite"></p><nav aria-label="Topics">{nav}</nav><p class="sidebar-note">Windows 11 · x64<br>English interface</p></aside>
<main id="main" class="main-content" tabindex="-1"><div class="page-heading"><p class="breadcrumb">Momo Monitor / User guide</p><h1>{p['title']}</h1><p class="page-desc">{p['description']}</p></div>{p['body']}{adjacent}<footer class="site-footer"><span>Momo Monitor 2.22.32 · Updated October 7, 2026</span><a href="https://tomideas.com/" target="_blank" rel="noopener">Designed by Tom Tam</a></footer></main><aside class="toc" aria-label="On this page"><strong>On this page</strong><nav></nav></aside></div></body></html>'''
        # Script-owned actions start disabled, so offline/no-JS reading never offers inert buttons.
        html = html.replace('<button ', '<button disabled ').replace('2.22.32', VERSION)
        html = html.replace('</head>', '<noscript><style>.sidebar{position:static;transform:none;visibility:visible;width:auto;border-right:0}.page-wrapper{display:block}.main-content{margin:0}.menu-toggle,.toc{display:none}</style></noscript></head>')
        (SITE / f'{p["slug"]}.html').write_text(html, encoding='utf-8')
    shutil.copyfile(ROOT / 'StatusMonitor/Assets/momo.png', SITE / 'assets/images/momo.png')
    fonts = SITE / 'assets/fonts'
    fonts.mkdir(exist_ok=True)
    for f in ['Geist-Regular.ttf', 'Geist-SemiBold.ttf', 'Geist-Bold.ttf', 'GeistMono-Regular.ttf']:
        shutil.copyfile(ROOT / 'StatusMonitor/Fonts' / f, fonts / f)
    ds = json.loads((ROOT / 'design-system/design-system.json').read_text(encoding='utf-8-sig'))
    keys = ['--ds-accent', '--ds-bg-page', '--ds-bg-surface', '--ds-bg-overlay', '--ds-border', '--ds-border-focus', '--ds-text-primary', '--ds-text-secondary', '--ds-on-accent', '--ds-interactive-hover', '--ds-interactive-pressed', '--ds-radius-sm', '--ds-radius-lg', '--ds-radius-xl', '--ds-space-7', '--ds-space-8', '--ds-space-10', '--ds-line-height']
    css = '/* Generated from design-system/design-system.json by dev/site/build-guide.py. */\n:root {\n'
    css += ''.join(f'  {k}: {ds["tokens"][k]};\n' for k in keys) + '}\n'
    (SITE / 'guide-tokens.css').write_text(css, encoding='utf-8')
    print(f'Built {len(PAGES)} English guide pages and the canonical token adapter.')

if __name__ == '__main__':
    build()
