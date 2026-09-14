# 规范扩展（Guidelines）

设计数据统一保存在 `../design-system.json`，展示入口是 `../index.html`。先在 JSON 的 guidelines 数组添加真实规范，页面会自动显示可编辑卡片；复杂视觉样本再按需拆分。AI 先读取 `../AI.md`。

只有符合以下任一条件时，才在本目录增加独立 HTML 样本：

- 同一基础规则已经包含多组需要并排比较的状态或主题；
- `index.html` 因内容过多而难以快速浏览；
- 某项规范需要独立分享、评审或视觉回归验证；
- 项目已有真实品牌、图标、动效或响应式规则需要详细记录。

新增样本时应包含：

1. 名称与用途（Name & Purpose）；
2. 真实代码或设计来源（Source）；
3. Observed／Proposed／Canonical 状态；
4. 可见样本（Visual Specimen）；
5. 使用边界与例外（Boundaries & Exceptions）；
6. 需要验证的页面与视口（Verification）。

不要为了填满目录而建立尚未使用的颜色、主题、断点或品牌规则。
