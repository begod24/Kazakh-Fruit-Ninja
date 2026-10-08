using UnityEngine;

namespace KazakhNinja
{
    static class AppBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Configure()
        {
            // Mobile defaults to 30 FPS; slicing needs a smooth blade.
            Application.targetFrameRate = 60;
        }
    }
}
