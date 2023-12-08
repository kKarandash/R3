﻿using R2.Internal;
using System.Runtime.CompilerServices;

namespace R2;

public interface IReadOnlyReactiveProperty<T> : IEvent<T>
{
    T Value { get; }
}

public interface IReactiveProperty<T> : IReadOnlyReactiveProperty<T>
{
    new T Value { get; set; }
}

public class ReactiveProperty<T> : IReactiveProperty<T>, IDisposable
{
    T value;
    IEqualityComparer<T>? equalityComparer;
    FreeListCore<Subscription> list;

    public IEqualityComparer<T>? EqualityComparer => equalityComparer;

    public T Value
    {
        get => this.value;
        set
        {
            if (EqualityComparer != null)
            {
                if (EqualityComparer.Equals(this.value, value))
                {
                    return;
                }
            }

            this.value = value;
            foreach (var subscriber in list.AsSpan())
            {
                subscriber?.OnNext(value);
            }
        }
    }

    public ReactiveProperty(T value)
        : this(value, EqualityComparer<T>.Default)
    {
    }

    public ReactiveProperty(T value, EqualityComparer<T>? equalityComparer)
    {
        this.value = value;
        this.equalityComparer = equalityComparer;
        this.list = new FreeListCore<Subscription>(this);
    }

    public IDisposable Subscribe(ISubscriber<T> subscriber)
    {
        var value = this.value;
        ObjectDisposedException.ThrowIf(list.IsDisposed, this);

        // raise latest value on subscribe
        subscriber.OnNext(value);

        var subscription = new Subscription(this, subscriber);
        subscription.removeKey = list.Add(subscription);
        return subscription;
    }

    void Unsubscribe(Subscription subscription)
    {
        list.Remove(subscription.removeKey);
    }

    public void Dispose()
    {
        list.Dispose();
    }

    public override string? ToString()
    {
        return (value == null) ? "(null)" : value.ToString();
    }

    sealed class Subscription(ReactiveProperty<T>? parent, ISubscriber<T> subscriber) : IDisposable
    {
        public int removeKey;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnNext(T message)
        {
            subscriber.OnNext(message);
        }

        public void Dispose()
        {
            var p = Interlocked.Exchange(ref parent, null);
            if (p == null) return;

            p.Unsubscribe(this);
        }
    }
}