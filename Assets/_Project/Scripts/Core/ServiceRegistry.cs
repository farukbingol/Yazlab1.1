using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yazlab.Core
{
    /// <summary>
    /// Pahalı <c>GameObject.Find</c> ve <c>FindObjectsByType</c> çağrılarını ortadan kaldırmak için
    /// kullanılan hafif, tip güvenli arayüz/servis kayıt merkezi.
    /// </summary>
    public static class ServiceRegistry
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        /// <summary>
        /// Belirtilen servis örneğini <typeparamref name="T"/> tipi altında kaydeder.
        /// </summary>
        /// <typeparam name="T">Kayıt anahtarı olacak arayüz veya sınıf tipi.</typeparam>
        /// <param name="instance">Kaydedilecek servis örneği.</param>
        public static void Register<T>(T instance) where T : class
        {
            if (instance == null)
            {
                Debug.LogWarning($"[ServiceRegistry] {typeof(T).Name} için null örnek kaydedilemez.");
                return;
            }

            Services[typeof(T)] = instance;
        }

        /// <summary>
        /// Belirtilen servis örneği halen kayıtlıysa kaydını siler.
        /// </summary>
        /// <typeparam name="T">Kayıt anahtarı olan arayüz veya sınıf tipi.</typeparam>
        /// <param name="instance">Kaydı silinecek örnek (<c>null</c> ise doğrudan tipi siler).</param>
        public static void Unregister<T>(T instance = null) where T : class
        {
            Type type = typeof(T);
            if (!Services.TryGetValue(type, out object existing))
            {
                return;
            }

            if (instance == null || ReferenceEquals(existing, instance))
            {
                Services.Remove(type);
            }
        }

        /// <summary>
        /// Kayıtlı servisi güvenli biçimde sorgular. Unity <see cref="UnityEngine.Object"/>
        /// örneklerinin yok edilmiş (destroyed) olup olmadığını da kontrol eder.
        /// </summary>
        /// <typeparam name="T">İstenen servis tipi.</typeparam>
        /// <param name="service">Bulunan servis örneği veya <c>null</c>.</param>
        /// <returns>Geçerli bir servis bulunduysa <c>true</c>.</returns>
        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out object raw) && raw is T typed)
            {
                if (typed is UnityEngine.Object unityObj && unityObj == null)
                {
                    Services.Remove(typeof(T));
                    service = null;
                    return false;
                }

                service = typed;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// Kayıtlı servisi döndürür; kayıtlı değilse <c>null</c> döner ve uyarı yazar.
        /// </summary>
        /// <typeparam name="T">İstenen servis tipi.</typeparam>
        /// <returns>Servis örneği veya <c>null</c>.</returns>
        public static T Get<T>() where T : class
        {
            if (TryGet<T>(out T service))
            {
                return service;
            }

            Debug.LogWarning($"[ServiceRegistry] {typeof(T).Name} servisi kayıtlı değil.");
            return null;
        }

        /// <summary>
        /// Belirtilen tipte geçerli bir servisin kayıtlı olup olmadığını döndürür.
        /// </summary>
        /// <typeparam name="T">Kontrol edilecek servis tipi.</typeparam>
        public static bool IsRegistered<T>() where T : class
        {
            return TryGet<T>(out _);
        }

        /// <summary>
        /// Tüm kayıtlı servisleri temizler.
        /// </summary>
        public static void Clear()
        {
            Services.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            Services.Clear();
        }
    }
}
