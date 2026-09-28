using ShellGame.Core; 
namespace ShellGame.Gameplay
{
    public interface IRoundInputTarget
    {
        void OnHoverEnter();
        void OnHoverExit();
        void Select(TurnSide selectedBy);
    }
}
