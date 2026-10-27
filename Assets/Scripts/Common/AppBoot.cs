using UnityEngine;
using UnityEngine.SceneManagement;

// 依場景名稱建立對應的遊戲：
//   Park     兒童樂園 (入口)
//   Main     注音摩天輪 (注音怪獸大作戰)
//   Multiply 九九乘法戰機
//   Island   卡皮巴拉發音島 (從 PhonicIsland 專案合併進來，Assets/PhonicsIsland)
// 場景本身都是空的，整個遊戲都由程式建立。
public static class AppBoot
{
    public const string ParkScene = "Park";
    public const string ZhuyinScene = "Main";
    public const string MultiplyScene = "Multiply";
    public const string IslandScene = "Island";

    static bool startupChecked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        startupChecked = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.targetFrameRate = 60;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 命令列 -scene <名稱>：直接進入某個場景 (自動測試用)
        if (!startupChecked)
        {
            startupChecked = true;
            string wanted = Arg("-scene");
            if (wanted != null && wanted != scene.name)
            {
                SceneManager.LoadScene(wanted);
                return;
            }
        }

        foreach (var c in Camera.allCameras) c.gameObject.SetActive(false);
        switch (scene.name)
        {
            case ParkScene: new GameObject("Park").AddComponent<ParkManager>(); break;
            case MultiplyScene: new GameObject("SkyShooter").AddComponent<SkyManager>(); break;
            case IslandScene: new GameObject("PhonicsIslandGame").AddComponent<PhonicsIsland.PhonicsIslandGame>(); break;
            default: new GameObject("PhonicShooter").AddComponent<GameManager>(); break;
        }
    }

    public static void Load(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public static string Arg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
