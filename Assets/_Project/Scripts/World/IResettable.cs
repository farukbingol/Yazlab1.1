namespace Yazlab.World
{
    /// <summary>
    /// Başlangıç durumuna anında geri döndürülebilen dünya nesnelerinin sözleşmesi
    /// (test tekrarları için şalter, kapı vb.).
    /// </summary>
    public interface IResettable
    {
        /// <summary>Nesneyi animasyonsuz olarak sahne başındaki durumuna döndürür.</summary>
        void ResetState();
    }
}
