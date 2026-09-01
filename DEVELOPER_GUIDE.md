# Metaloc VPS SDK 接入指南

**版本 1.0.0 | 适用设备：XREAL Ultra / XREAL One**

---

## 概述

本 SDK 为 XREAL AR 眼镜提供 6DoF 视觉定位能力，接入 Metaloc VPS 云服务后，您的应用将：

1. 自动采集灰度相机图像
2. 上传至 Metaloc VPS 服务器进行识别
3. 定位成功后，将您的 AR 内容自动放置到现实世界的正确位置

**您不需要编写任何定位算法代码**，只需：
- 填写一个配置文件（服务器地址、Token、地图名称）
- 配置一个场景清单文件（您的 AR 物体列表及其坐标）
- 将 3D 模型打包为 AssetBundle 并推送到设备

---

## 环境要求

| 条件 | 最低要求 |
|---|---|
| Unity 版本 | 2021.3 LTS |
| Unity 模块 | Android Build Support（含 Android SDK & NDK） |
| Unity 包 | Input System 1.4+（Package Manager 内置，需单独启用） |
| XREAL XR Plugin | 2.0.0+（需从 XREAL 开发者中心单独下载安装） |
| 构建平台 | Android（arm64-v8a，IL2CPP） |
| 设备 | XREAL Ultra / XREAL One |
| Android 权限 | `CAMERA`、`INTERNET`、`com.xreal.permission.EYE_TRACKING` |

---

## 第一步：准备 Unity 工程环境

### 1a. 安装 Unity Android Build Support

在 **Unity Hub** 中：

1. 找到您使用的 Unity 版本 → 右侧齿轮图标 → **Add Modules**
2. 勾选 **Android Build Support**，展开后同时勾选 **Android SDK & NDK Tools** 和 **OpenJDK**
3. 点击 **Install** 等待完成

### 1b. 启用 Input System Package

1. 打开 **Window → Package Manager**
2. 左上角下拉选 **Unity Registry**，搜索 **Input System**
3. 点击 **Install**
4. Unity 会弹出对话框询问是否切换 Input System → 点击 **Yes**（Unity 会重启）

### 1c. 安装 XREAL XR Plugin

XREAL SDK 需要从 XREAL 开发者中心单独获取，不在 Unity 官方 Package Registry 中。

1. 前往 [XREAL 开发者中心](https://developer.xreal.com) 下载 XREAL XR Plugin（`.unitypackage` 或 git URL）
2. 按 XREAL 官方文档安装插件
3. 安装完成后，进入 **Edit → Project Settings → XR Plug-in Management → Android** 标签页，勾选 **XREAL**

### 1d. 申请并配置 XREAL Enterprise License

> **灰度相机是 XREAL Enterprise API**，必须有绑定您应用包名的 License 文件才能正常工作。没有 License，相机初始化会静默失败，导致永远无法定位。

**申请步骤：**

1. 在 [XREAL 开发者中心](https://developer.xreal.com) 注册账号，申请 Enterprise 开发者权限
2. 创建应用，填写您的应用包名（即 Unity Player Settings 中的 **Package Name**，例如 `com.yourcompany.yourapp`）
3. 下载分配给该应用的 License 文件（通常是一个加密的 `.txt` 文件）

**配置步骤：**

1. 将下载的 License 文件放入 Unity 工程的 `Assets/XR/Settings/` 目录下
2. 打开 **Edit → Project Settings → XR Plug-in Management → XREAL** 设置页
3. 找到 **License Asset** 字段，将 License 文件拖入该字段

> **注意**：License 与包名绑定，换包名必须重新申请。不同开发者必须各自申请，不能共用同一个 License 文件。

### 1e. 安装 Metaloc VPS SDK

**方式 A：从 Git 地址安装（推荐）**

在 Unity 中打开 **Window → Package Manager**，点击左上角 **"+"**，选择 **Add package from git URL**，输入：

```
https://github.com/yuancaimaiyi/metaloc-xreal-vps-sdk.git
```

安装完成后，Package Manager 列表中会出现 **Metaloc VPS SDK**。

**方式 B：从本地文件夹安装（收到 ZIP 包时）**

1. 解压收到的 SDK 压缩包
2. Unity 中：**Window → Package Manager → "+" → Add package from disk**
3. 找到解压后的文件夹，选择 `package.json`，点击 **Open**

### 1f. 配置 AndroidManifest 权限

XREAL 灰度相机需要企业级权限，默认 AndroidManifest 不包含，需手动添加：

1. 在 Project 面板中依次创建目录：`Assets/Plugins/Android/`
2. 在该目录下新建文本文件，命名为 **`AndroidManifest.xml`**
3. 将以下内容粘贴进去：

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <uses-permission android:name="android.permission.CAMERA" />
    <uses-permission android:name="android.permission.INTERNET" />
    <uses-permission android:name="com.xreal.permission.EYE_TRACKING" />
    <application>
        <activity android:name="com.unity3d.player.UnityPlayerActivity"
                  android:theme="@style/UnityThemeSelector">
            <intent-filter>
                <action android:name="android.intent.action.MAIN" />
                <category android:name="android.intent.category.LAUNCHER" />
            </intent-filter>
        </activity>
    </application>
</manifest>
```

> 如果 XREAL SDK 安装后已自动生成 AndroidManifest.xml，只需在其 `<manifest>` 标签内追加上面三行 `<uses-permission>` 即可，不要重复创建文件。

---

## 第二步：创建 VPS 配置资产

1. 在 Project 面板中右键 → **Create → Metaloc → VPS Config**
2. 命名为 `MyVPSConfig`，保存在 Assets 下任意位置
3. 选中该文件，在 Inspector 中填写以下字段：

| 字段 | 填写内容 |
|---|---|
| **Vps Api Url** | Metaloc 提供的服务器地址 |
| **Auth Token** | 您的鉴权 Token（Metaloc 颁发，以 `Bearer ey...` 开头） |
| **Cookie Header** | 通常留空，如有要求 Metaloc 客服会告知 |
| **Map Name** | 您的地图 ID（Metaloc 建图完成后提供） |
| **Target Eye** | `Right`（默认，使用右眼摄像头） |
| 其余字段 | 保持默认值即可 |

> **注意：不要将 Auth Token 提交到公开代码仓库。**

---

## 第三步：制作 AR 内容

> **核心思路**：Metaloc 提供的稠密点云与定位地图处于同一坐标系。在 Unity 中将点云作为参照物摆放好模型后，直接读取模型在 Unity 中的位置坐标，这个坐标就是填入 SDK 的 `vpsPosition`。

---

### 3a. 将稠密点云导入 Unity

Metaloc 交付的稠密点云文件通常为 `.ply` 格式，需要借助插件在 Unity 中显示。

**推荐方式：将点云转换为网格（Mesh）后导入**

1. 用 **MeshLab** 或 **CloudCompare** 打开 `.ply` 文件
2. 对点云做适当降采样（大场景建议保留 50 万点以内，否则 Unity 卡顿）
3. 导出为 `.obj` 格式（File → Export Mesh → OBJ）
4. 将 `.obj` 拖入 Unity Project 面板，Unity 会自动导入为 Mesh
5. 将导入后的模型拖入场景 Hierarchy，命名为 `PointCloudRef`

**或者使用 Unity Asset Store 插件**

在 Asset Store 搜索 **"Point Cloud Viewer"**，安装后可直接加载 `.ply` 文件，无需转换。

> **重要**：导入点云时不要修改任何坐标轴方向，保持原始坐标不变，才能保证后续读取的位置与 VPS 坐标一致。

---

### 3b. 导入 3D 模型并在点云上摆放

1. 将您的 3D 模型（`.fbx`、`.glb` 等）导入 Unity
2. 调整材质、缩放至正确比例
3. 将模型拖入 Hierarchy，参照点云在场景中移动到您希望呈现的位置
4. 调整旋转角度，使模型朝向与现实方向一致

---

### 3c. 读取 VPS 坐标

模型摆放好后，在 Hierarchy 中选中该模型，**Inspector 面板的 Transform 组件**会显示其位置：

```
Transform
  Position   X: 3.2    Y: 0.0    Z: -1.5
  Rotation   X: 0      Y: 90     Z: 0
  Scale      X: 1      Y: 1      Z: 1
```

**这里的 Position 就是 `vpsPosition`，Rotation 就是 `vpsEulerAngles`**，后续直接填入场景清单。

> **验证方法**：如果上线后模型位置偏差较大，说明点云导入时坐标轴被翻转了。解决方法：将读出的 Z 值取反后重新填写 `vpsPosition.z`。

---

### 3d. 将模型制作成 Prefab 并设置 AssetBundle 标签

1. 在 Hierarchy 中选中摆放好的模型，拖拽到 Project 面板生成 **Prefab**
2. 记下 Prefab 名称（例如 `MyBuilding`），区分大小写
3. 在 Project 面板中选中该 Prefab，Inspector 面板**最底部**找到 **AssetBundle** 下拉框
4. 点击下拉 → **New...** → 输入名称（例如 `my_building`，建议只用小写字母和下划线）

> 场景中用于参照的点云 `PointCloudRef` **不要设置** AssetBundle 标签，它只是摆放时的参考，不需要打包。

---

### 3e. 打包 AssetBundle

> **打包前必须先切换平台**：依次点击 **File → Build Settings → Android → Switch Platform**，等待平台切换完成。如果平台是 Windows 或 macOS，打出的 Bundle 文件无法在 Android 设备上加载。

Unity 没有内置的 AssetBundle 构建菜单，需要先创建一个 Editor 脚本：

1. 在 Unity Project 面板中，在 `Assets/` 下新建文件夹，命名为 **`Editor`**
2. 在该文件夹内右键 → **Create → C# Script**，命名为 **`BuildBundles`**
3. 将脚本内容替换为以下代码：

```csharp
using UnityEditor;
using System.IO;

public class BuildBundles
{
    [MenuItem("Assets/Build AssetBundles")]
    static void Build()
    {
        string outDir = "AssetBundles/Android";
        Directory.CreateDirectory(outDir);
        BuildPipeline.BuildAssetBundles(outDir,
            BuildAssetBundleOptions.None,
            BuildTarget.Android);
        UnityEditor.EditorUtility.DisplayDialog("完成", "AssetBundle 构建完毕：" + outDir, "OK");
    }
}
```

4. 保存后回到 Unity，等待编译完成
5. Unity 菜单中会出现 **Assets → Build AssetBundles**，点击执行
6. 构建完成后，在项目根目录的 `AssetBundles/Android/` 文件夹中找到名为 `my_building` 的文件（无扩展名），这就是 Bundle 文件

> **注意**：`Editor/` 文件夹必须直接在 `Assets/` 下，否则 `[MenuItem]` 不会生效。

---

## 第四步：创建场景清单

1. 在 Project 面板中右键 → **Create → Metaloc → Scene Manifest**
2. 命名为 `MySceneManifest`
3. 在 Inspector 中填写：

| 字段 | 填写内容 |
|---|---|
| **Asset Bundle Sub Folder** | Bundle 文件在设备上的子目录名（例如 `mymap_bundles`） |
| **Entries** | AR 物体列表，点击右下角 **"+"** 逐条添加 |

每条 Entry 的字段：

| 字段 | 示例值 | 说明 |
|---|---|---|
| **Id** | `my_building` | 唯一标识符，建议与 Bundle 名一致 |
| **Asset Bundle Name** | `my_building` | Bundle 文件名（第三步 3d 设置的 AssetBundle 标签） |
| **Prefab Name** | `MyBuilding` | Bundle 内 Prefab 的名称（区分大小写） |
| **Vps Position** | `(3.2, 0.0, -1.5)` | 在 Unity 点云场景中摆放模型后，从 Inspector Transform 读取的 Position |
| **Vps Euler Angles** | `(0, 90, 0)` | 从 Inspector Transform 读取的 Rotation（Y 轴为偏航角，单位度） |
| **Local Scale** | `(1, 1, 1)` | 物体缩放比例 |

---

## 第五步：搭建场景

### 5a. 添加 SDK 组件

1. 在场景 Hierarchy 中右键 → **Create Empty**，命名为 `MetalocVPS`
2. 选中该物体，点击 **Add Component**，分别搜索并添加：
   - **Metaloc VPS Manager**
   - **Metaloc AR Content Manager**
3. 赋值：
   - `MetalocVPSManager → Config` 字段 → 拖入 `MyVPSConfig`
   - `MetalocARContentManager → Manifest` 字段 → 拖入 `MySceneManifest`

### 5b. 编写启动代码

在 `MetalocVPS` 物体上再添加一个脚本，负责启动定位并接收回调：

```csharp
using UnityEngine;
using Metaloc.VPS;

public class MyApp : MonoBehaviour
{
    private MetalocVPSManager m_VPS;

    void Start()
    {
        m_VPS = GetComponent<MetalocVPSManager>();

        // 定位成功时触发（每次位置更新都会触发，包括热启动校正）
        m_VPS.OnLocalized += OnLocalized;

        // 定位失败时触发（网络错误、识别失败等）
        m_VPS.OnLocalizationFailed += OnFailed;

        // 开始定位（场景加载完成后调用）
        m_VPS.StartLocalization();
    }

    void OnLocalized(VPSLocalizationResult result)
    {
        // AR 内容由 MetalocARContentManager 自动移动到正确位置
        // 在这里添加您自己的业务逻辑，例如显示定位成功 UI
        Debug.Log("定位成功！识别得分 = " + result.rawResponse.score);
    }

    void OnFailed(string reason)
    {
        Debug.LogWarning("定位失败：" + reason);
    }

    void OnDestroy()
    {
        if (m_VPS != null)
        {
            m_VPS.OnLocalized -= OnLocalized;
            m_VPS.OnLocalizationFailed -= OnFailed;
        }
    }
}
```

### 5c. 完整场景结构示意

```
Hierarchy
└── MetalocVPS (GameObject)
    ├── MetalocVPSManager      ← 拖入 MyVPSConfig
    ├── MetalocARContentManager ← 拖入 MySceneManifest
    └── MyApp                  ← 您的启动脚本
```

---

## 第六步：将 Bundle 文件推送到设备

### 开启 XREAL 眼镜的 USB 调试

1. 将 XREAL 眼镜通过 USB-C 线连接电脑
2. 在眼镜系统设置中开启 **开发者选项** → **USB 调试**（具体路径参考 XREAL 设备说明书）
3. 电脑终端运行 `adb devices`，确认眼镜出现在设备列表中

### 开发阶段：使用 ADB 推送

```bash
# 将包名替换为您应用的实际包名（在 Player Settings → Other Settings → Package Name 中查看）
adb push ./AssetBundles/Android/my_building \
    /sdcard/Android/data/com.yourcompany.yourapp/files/mymap_bundles/my_building
```

设备上的完整路径为：
```
/data/user/0/<包名>/files/<Asset Bundle Sub Folder>/<Bundle文件名>
```

### 正式发布：应用内下载

在应用启动时，从您的 CDN 下载 Bundle 文件并保存到 `Application.persistentDataPath + "/mymap_bundles/"` 目录下。SDK 会自动从该路径读取。

---

## 第七步：构建并运行

### 7a. 配置 Player Settings

打开 **Edit → Project Settings → Player → Android** 标签页，按下表配置：

| 设置项 | 位置 | 必填值 |
|---|---|---|
| **Scripting Backend** | Other Settings | **IL2CPP** |
| **Target Architectures** | Other Settings | 只勾选 **ARM64** |
| **Minimum API Level** | Other Settings | **Android 8.0（API 26）** 或更高 |
| **Active Input Handling** | Other Settings | **Both**（同时支持新旧 Input System） |
| **Package Name** | Other Settings | 您的应用包名，例如 `com.yourcompany.yourapp` |

> **Active Input Handling 必须设为 Both 或 Input System Package (New)**，否则 SDK 的陀螺仪会无法工作，导致定位请求永远不发出。

### 7b. 构建 APK

1. **File → Build Settings** → 确认平台为 **Android**
2. **Project Settings → XR Plug-in Management → Android** → 确认 XREAL 已勾选
3. 点击 **Build** 生成 APK，或 **Build And Run** 直接安装到已连接的 XREAL 设备

### 7c. 运行测试

1. 佩戴 XREAL 眼镜，朝向已建图区域
2. 等待约 **5 秒**（首次定位，设备保持相对静止效果更好）
3. 定位成功后，AR 内容自动出现在正确位置
4. 在 Android Logcat（或 Unity Console 通过 adb logcat）中可以看到 `[MetalocVPSManager] Posidon accepted fusion` 日志，确认定位成功

---

## API 速查

### MetalocVPSManager

| 成员 | 类型 | 说明 |
|---|---|---|
| `Config` | MetalocVPSConfig | 赋值您的配置资产 |
| `IsLocalized` | bool（只读） | 是否已完成首次定位 |
| `VpsToUnityMatrix` | Matrix4x4 | 当前 VPS→Unity 变换矩阵 |
| `OnLocalized` | event | 每次定位成功/更新时触发 |
| `OnLocalizationFailed` | event | 定位失败时触发 |
| `StartLocalization()` | 方法 | 开始定位轮询 |
| `StopLocalization()` | 方法 | 暂停定位 |
| `TriggerVPSRequest()` | 方法 | 立即强制触发一次定位请求 |
| `SetMapName(string)` | 方法 | 运行时切换地图名称 |
| `VpsToUnity(Vector3)` | 方法 | 将 VPS 坐标转换为 Unity 世界坐标 |

### MetalocARContentManager

| 成员 | 类型 | 说明 |
|---|---|---|
| `Manifest` | MetalocSceneManifest | 赋值您的场景清单 |
| `smoothCorrection` | bool | 热启动校正时是否平滑移动（默认 true） |
| `smoothDuration` | float | 平滑移动持续时间，单位秒（默认 0.5） |
| `ReloadScene()` | 方法 | 卸载全部内容并重新加载（切换地图时调用） |

---

## 常见问题

**Q：定位成功后 AR 物体没有出现**

检查 Console 是否有 `[MetalocARContentManager] Failed to load bundle`。确认：
- Bundle 文件确实存在于设备的对应路径
- `Asset Bundle Sub Folder` 填写正确
- `Prefab Name` 和 Bundle 内的 Prefab 名称完全一致（区分大小写）

**Q：Console 报 HTTP 401 错误，始终无法定位**

Auth Token 无效或已过期，请联系 Metaloc 获取新 Token。

**Q：AR 物体出现了但位置偏差很大**

按以下顺序排查：
1. 确认 `Map Name` 与 Metaloc 服务端注册的地图 ID 完全一致
2. 如果用稠密点云在 Unity 中摆放模型：确认点云导入时没有修改坐标轴方向。若偏差主要体现在深度方向（前后颠倒），尝试将 `vpsPosition.z` 取反后重新填写
3. 如果位置整体平移而非旋转偏差，检查摆放时使用的点云文件与当前建图版本是否一致

**Q：SDK 多久发一次定位请求？**

首次定位前：每 5 秒发一次（设备静止时）。首次定位成功后：移动超过 5 米或距上次成功超过 60 秒时触发。

**Q：如何在运行时切换到另一张地图？**

```csharp
m_VPS.SetMapName("new_map_id");
m_ContentManager.ReloadScene();
// 确保新地图的 Bundle 文件已推送到设备
```

**Q：可以同时显示多个 AR 物体吗？**

可以。在场景清单的 Entries 列表中添加多条记录即可，每条对应一个独立物体。

---

## 接入检查清单

在设备上测试前，请逐项确认：

**环境**
- [ ] Unity Android Build Support 模块已安装（含 SDK & NDK）
- [ ] Input System Package 已安装
- [ ] XREAL XR Plugin 已安装并在 XR Plug-in Management → Android 中勾选

**Player Settings**
- [ ] Scripting Backend = IL2CPP
- [ ] Target Architectures 仅勾选 ARM64
- [ ] Active Input Handling = Both 或 Input System Package (New)
- [ ] Minimum API Level ≥ 26

**权限与配置**
- [ ] XREAL Enterprise License 文件已放入工程并绑定到 XR Plug-in Management → XREAL → License Asset
- [ ] License 绑定的包名与 Player Settings → Package Name 完全一致
- [ ] AndroidManifest.xml 包含 `com.xreal.permission.EYE_TRACKING` 权限
- [ ] Auth Token 已填写（以 `Bearer ` 开头，注意有空格）
- [ ] Map Name 与 Metaloc 服务端注册的地图 ID 完全一致

**AB 包**
- [ ] 构建 Bundle 前已切换 Build Settings 平台为 Android
- [ ] Bundle 文件已推送到设备的正确路径
- [ ] Asset Bundle Sub Folder 与 adb push 路径中的子目录名完全一致
- [ ] Prefab Name 大小写与 Bundle 内一致

**运行**
- [ ] XREAL 眼镜已开启 USB 调试
- [ ] 在真实 XREAL 设备上运行（Unity Editor 内无法调用灰度相机）

---

## 联系支持

如遇到接入问题，请发邮件至 **support@metaloc.cn**，并附上：
- Unity 版本
- XREAL XR Plugin 版本
- Console 错误截图或完整日志

*SDK 版本 1.0.0 | 2026*
