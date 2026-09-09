using Pulse.Domain;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Pulse.Input
{
    public interface IGameplayInputProvider { GameplayActions Poll(); }

    public sealed class DesktopInputProvider : IGameplayInputProvider
    {
        public GameplayActions Poll()
        {
            bool overUI=EventSystem.current!=null && EventSystem.current.IsPointerOverGameObject();
            return new GameplayActions(
                UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) ||
                (UnityEngine.Input.GetMouseButtonDown(0) && !overUI),
                UnityEngine.Input.GetKeyDown(KeyCode.Escape), UnityEngine.Input.GetKeyDown(KeyCode.R));
        }
    }

    public sealed class TouchInputProvider : IGameplayInputProvider
    {
        public GameplayActions Poll()
        {
            bool primary=false;
            for(int i=0;i<UnityEngine.Input.touchCount;i++)
            {
                var touch=UnityEngine.Input.GetTouch(i);
                if(touch.phase==TouchPhase.Began && (EventSystem.current==null || !EventSystem.current.IsPointerOverGameObject(touch.fingerId))) primary=true;
            }
            return new GameplayActions(primary);
        }
    }
}
