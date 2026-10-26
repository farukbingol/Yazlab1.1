namespace Yazlab.Companion
{
    /// <summary>
    /// Köpek refakatçi durum makinesindeki tek bir durumun sözleşmesi.
    /// </summary>
    public interface ICompanionState
    {
        /// <summary>Loglarda ve hata ayıklamada görünen durum adı.</summary>
        string Name { get; }

        /// <summary>Duruma girildiğinde bir kez çağrılır.</summary>
        void Enter();

        /// <summary>Durum aktifken her karede çağrılır.</summary>
        /// <param name="dt">Son kareden geçen süre (sn).</param>
        void Tick(float dt);

        /// <summary>Durumdan çıkılırken bir kez çağrılır.</summary>
        void Exit();
    }
}
