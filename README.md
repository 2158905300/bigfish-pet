# BigFishPet 大肥鱼桌宠

一只傲娇甜的鲸鱼娘桌面宠物，C# 原生 WPF 编写，单文件零依赖，Windows 直接跑。

![whale](assets/expressions/vf_happy.png)

## 功能

- 单击鲸鱼：抖动 + 随机台词 + 切换表情（台词池洗牌，一轮内不重复）
- 双击鲸鱼：打开 AI 聊天框，回车发送，和鲸鱼娘聊天（可选功能）
- 拖拽：按住鲸鱼拖动，可以放到屏幕任何位置
- 右键菜单：刷新数据 / 放大 / 缩小 / 退出
- 自动气泡：每 45 秒随机冒一句台词（时段提示、今日消耗、余额等）
- 表情智能匹配：台词内容自动匹配对应表情（生气、害羞、开心、震惊…）

## 快速开始

### 方式一：直接编译运行

需要 Windows + .NET Framework 4.x（Win10/11 自带），不需要安装任何东西：

```
build.bat
BigFishPet.exe
```

把 `assets\expressions\` 文件夹（内含 vf_*.png 表情图）放在 exe 同目录即可。

### 方式二：开启 AI 聊天（可选，需要 DeepSeek API Key）

桌宠本体（台词、表情、拖拽）不需要任何配置，双击就能玩。只有"双击鲸鱼聊天"这个功能需要 API Key。

#### 第一步：拿 API Key

1. 打开 DeepSeek 开放平台：https://platform.deepseek.com
2. 注册 / 登录账号（手机号即可）
3. 左侧菜单选 **API Keys** -> **创建 API Key**
4. 复制生成的 Key（`sk-` 开头的一长串，只显示一次，注意保存）
5. （首次使用需充值少量金额，聊天走便宜模型，聊几句几分钱）

#### 第二步：配置 Key（任选一种）

**方法 A：一键脚本（最简单，推荐）**

双击项目里的 **`setup-ai-key.bat`**，粘贴 Key 回车，搞定。

**方法 B：图形界面**

1. 按 `Win + R`，输入 `sysdm.cpl` 回车
2. 「高级」标签 -> 「环境变量」
3. 在「用户变量」里点「新建」：变量名填 `DEEPSEEK_API_KEY`，变量值粘贴你的 Key
4. 一路点「确定」

**方法 C：命令行**

```
setx DEEPSEEK_API_KEY "sk-粘贴你的key"
```

#### 第三步：重启桌宠

关掉 `BigFishPet.exe` 再重新双击启动（环境变量只在程序启动时读一次）。
如果刚配完还是提示"没配 key"，注销或重启一次电脑再试。

#### 验证是否配好

新开一个命令行窗口，输入：

```
echo %DEEPSEEK_API_KEY%
```

能显示 `sk-` 开头的 Key 就是配好了。

> 没有配置 Key 时桌宠完全正常，只是双击聊天会提示"没配 key，吃不上白饭"。

> 注意：AI 聊天消耗的是**你自己的** DeepSeek API 额度（默认走便宜的 flash 模型）。**不要把你的 Key 发给别人**，谁聊天烧谁的钱。

### 语音朗读（默认日语 Galgame 模式，双引擎）

AI 聊天回复时，桌宠会把回答**用语音念出来**（说完即焚，不留文件）。

- **默认日语模式**：AI 用**日语台词**回复（气泡里附一行『中文翻译』），语音朗读——Galgame 体验
- **双语音引擎（自动切换）**：
  - 首选：**VOICEVOX 本地合成**（ずんだもん等角色声线，默认ずんだもん）——本地快、免费、无断流；引擎没开时桌宠**自动后台拉起**（vv-engine run.exe 隐藏运行，启动时预热）
  - 兜底：**edge-tts 在线**（日语七海 Nanami / 中文萝莉 Xiaoyi）
- 右键鲸鱼菜单：`语音：开/关`、`模式`（日语 ↔ 中文）
- VOICEVOX 引擎位置探测：环境变量 `VV_ENGINE_EXE` → 标准安装目录 → 解压目录（见下方安装教程）
- 找不到任何语音依赖时**自动静音**，桌宠其他功能不受影响

#### 想用"俊达萌"等 VOICEVOX 角色声线？（可选，几步搞定）

默认情况下没有 VOICEVOX 也能出声（走 edge-tts 在线），但想要 **ずんだもん（俊达萌）** 这种本地角色声线，需要装 VOICEVOX 语音引擎。桌宠检测到引擎后**自动使用、自动静默后台运行**（不需要开 VOICEVOX 窗口）。

**第一步：下载 VOICEVOX（约 1.9GB）**

选择适合你电脑的版本（Windows）：

- 有 NVIDIA 显卡：`voicevox-windows-directml-*.zip`（走 GPU）
- 没有独显/AMD/Intel：`voicevox-windows-cpu-*.zip`（CPU 也能跑）

下载地址（选一个快的）：
- 官方 GitHub Releases：https://github.com/VOICEVOX/voicevox/releases/latest
- 国内加速镜像（把下面地址前缀加上）：
  ```
  https://ghproxy.com/https://github.com/VOICEVOX/voicevox/releases/download/0.25.2/voicevox-windows-cpu-0.25.2.zip
  ```
  （版本号以 Releases 页最新为准；`ghproxy.com` 也可换 `ghfast.top`、`gh-proxy.com` 等镜像）

**第二步：解压**

用解压软件把 zip 解压到任意目录（比如 `D:\voicevox`），解压后里面有个 `VOICEVOX\vv-engine\run.exe` —— 这就是语音引擎本体。

**第三步：告诉桌宠引擎在哪（二选一）**

- 方式 A（推荐）：把 `vv-engine` 文件夹整个复制到桌宠 exe 同目录下，变成：
  ```
  你的桌宠文件夹/
  ├── BigFishPet.exe
  └── vv-engine/
      └── run.exe
  ```
- 方式 B：设置环境变量指向引擎：
  ```
  setx VV_ENGINE_EXE "D:\voicevox\VOICEVOX\vv-engine\run.exe"
  ```
  （设完重启桌宠）

**第四步：完成！**

桌宠启动时会**自动后台静默拉起引擎**（隐藏窗口运行，不打扰你），然后 AI 聊天就用俊达萌的声音朗读日语台词了。引擎没找到/没装时自动退回 edge-tts 在线语音，不会报错。

> 小知识：VOICEVOX 的角色声线都是本地合成，**不消耗任何 API 费用、不依赖网络**；想换角色（四国めたん、雨晴はう等）可以改源码里的 `VvSpeaker` 数字（3=俊达萌ノーマル，其他 ID 见 VOICEVOX 角色列表）。

### 数据统计（可选）

桌宠会显示 DeepSeek 峰谷时段和余额。今日消耗统计依赖本机 dsh（DeepSeek Harness）的会话日志和 zstd 工具，普通用户没有这些环境时会自动跳过，不影响使用。

## 交互与外观

- 自绘白底深蓝描边气泡，三级信息排版
- 支持放大缩小（右键菜单）
- 崩溃日志写在 exe 同目录 `bigfish-crash.log`

## 目录结构

```
bigfish-pet/
├── BigFishPet.cs              # 全部源码（单文件）
├── build.bat                  # 编译脚本（csc，免安装）
├── setup-ai-key.bat           # 一键配置 AI 聊天 Key（双击运行）
├── assets/
│   └── expressions/           # 表情素材（vf_*.png）
├── voice/
│   ├── tts-cli.mjs            # edge-tts 语音 CLI（在线兜底）
│   └── vv-cli.mjs             # VOICEVOX 本地语音 CLI（首选）
└── README.md
```

## 许可

- 代码：MIT License，见 [LICENSE](LICENSE)
- 表情素材：收集自网络 / 视频抠图，版权归原作者所有，仅供学习交流；如需商用请自行确认版权。素材启发自开源社区项目 [dsh-dafeiyu](https://github.com/nolodjska/dsh-dafeiyu) 等鲸鱼娘桌宠作品。

## 免责声明

本项目是个人学习作品，作者不对使用本项目产生的任何 API 费用、系统问题或法律风险负责。
