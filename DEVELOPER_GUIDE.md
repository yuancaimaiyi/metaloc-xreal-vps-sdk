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
| XREAL XR Plugin | 2.0.0+（需从 XREAL 开发者中心单独下载安装） |
| 构建平台 | Android（arm64-v8a） |
| 设备 | XREAL Ultra / XREAL One |
| Android 权限 | `CAMERA`、`INTERNET`、`com.xreal.permission.EYE_TRACKING` |

---

## 第一步：安装 SDK

### 方式 A：从 Git 地址安装（推荐）

在 Unity 中打开 **Window → Package Manager**，点击左上角 **"+"**，选择 **Add package from git URL**，输入：

```
https://github.com/metaloc/xreal-vps-sdk.git
```

安装完成后，Package Manager 列表中会出现 **Metaloc VPS SDK**。

### 方式 B：从本地文件夹安装（收到 ZIP 包时）

1. 解压收到的 SDK 压缩包
2. Unity 中：**Window → Package Manager → "+" → Add package from disk**
3. 找到解压后的文件夹，选择 `package.json`，点击 **Open**

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

### 3a. 在 Unity 中制作 Prefab

1. 将您的 3D 模型（`.fbx`、`.glb` 等）导入 Unity
2. 调整材质、缩放至正确比例
3. 在 Hierarchy 面板中选中模型，拖拽到 Project 面板创建 **Prefab**
4. 记下 Prefab 的名称（例如 `MyBuilding`），后续需要用到

### 3b. 给 Prefab 设置 AssetBundle 标签

1. 在 Project 面板中选中 Prefab
2. Inspector 面板**最底部**找到 **AssetBundle** 下拉框
3. 点击下拉 → **New...** → 输入名称（例如 `my_building`）

> Bundle 名称建议只用小写字母和下划线，不要含空格。

### 3c. 打包 AssetBundle

1. Unity 菜单 → **Assets → Build AssetBundles**
2. 平台选择 **Android**
3. 构建完成后，在输出目录（例如 `AssetBundles/Android/`）找到名为 `my_building` 的文件（无扩展名），这就是 Bundle 文件

### 3d. 获取 VPS 坐标

Metaloc 在交付地图数据时，会同时提供**锚点坐标表**，列出每个参考点在 VPS 世界坐标系下的三维坐标（单位：米）。

这就是下一步中 `vpsPosition` 的填写依据。例如某建筑入口的坐标是 `(3.2, 0.0, -1.5)`，则填写该值。

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
| **Asset Bundle Name** | `my_building` | Bundle 文件名（第三步 3b 设置的标签） |
| **Prefab Name** | `MyBuilding` | Bundle 内 Prefab 的名称（区分大小写） |
| **Vps Position** | `(3.2, 0.0, -1.5)` | VPS 坐标系下的位置（来自 Metaloc 锚点表） |
| **Vps Euler Angles** | `(0, 90, 0)` | VPS 坐标系下的旋转（Y 轴为偏航角，单位度） |
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

### 开发阶段：使用 ADB

```bash
# 将包名替换为您应用的实际包名
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

1. **File → Build Settings** → 切换平台为 **Android**
2. **Project Settings → XR Plug-in Management** → 确认 XREAL 已勾选
3. 构建 APK 并安装到 XREAL 设备
4. 启动应用 → 佩戴眼镜，朝向已建图区域 → 等待约 5 秒（首次定位）
5. 定位成功后，AR 内容出现在正确位置

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

检查 `vpsPosition` 是否来自 Metaloc 提供的锚点坐标表，不能填写 Unity 世界坐标。确认使用的地图名称（Map Name）与建图时一致。

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

- [ ] XREAL XR Plugin 已安装并在 XR Plug-in Management 中勾选
- [ ] AndroidManifest.xml 包含 `com.xreal.permission.EYE_TRACKING` 权限
- [ ] Auth Token 已填写（以 `Bearer ` 开头，注意末尾有空格）
- [ ] Map Name 与 Metaloc 服务端注册的地图 ID 完全一致
- [ ] Bundle 文件已推送到设备的正确路径
- [ ] Prefab Name 大小写与 Bundle 内一致
- [ ] 在真实 XREAL 设备上运行（Unity Editor 内无法调用灰度相机）

---

## 联系支持

如遇到接入问题，请发邮件至 **support@metaloc.cn**，并附上：
- Unity 版本
- XREAL XR Plugin 版本
- Console 错误截图或完整日志

*SDK 版本 1.0.0 | 2026*
