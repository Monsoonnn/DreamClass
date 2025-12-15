using System;

namespace Systems.SceneManagement
{
    public interface ISceneLoadNotifier
    {
        event Action OnSceneLoadComplete;
    }
}
