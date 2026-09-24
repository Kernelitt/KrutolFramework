using System;
using System.Collections.Generic;
using System.Text;

namespace KrutolFramework
{
    public interface IScene
    {
        void Initialize();
        void Update(float deltaTime);
        void Render(SpriteBatch batch);
        void Destroy();
    }

    public static class SceneManager
    {
        public static IScene CurrentScene { get; private set; }

        public static void SwitchScene(IScene newScene)
        {
            CurrentScene?.Destroy();
            CurrentScene = newScene;
            CurrentScene?.Initialize();
        }

        public static void Update(float deltaTime) => CurrentScene?.Update(deltaTime);
        public static void Render(SpriteBatch batch) => CurrentScene?.Render(batch);
    }
}
