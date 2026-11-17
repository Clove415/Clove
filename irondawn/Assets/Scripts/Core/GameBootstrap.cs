using UnityEngine;
using Object = UnityEngine.Object;

namespace IronDawn
{
    /// <summary>
    /// 入口。不需要任何场景内容：Unity 一进入 Play，这里就把对战场地搭出来并显示联机菜单。
    /// 所以工程里没有任何 .unity 场景资源也能跑。
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<ArenaRuntime>() != null) return;

            GameObject go = new GameObject("[IronDawn Runtime]");
            go.AddComponent<ArenaRuntime>();
        }
    }
}
