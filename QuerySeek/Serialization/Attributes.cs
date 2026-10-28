using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using MessagePack.Formatters;

namespace QuerySeek.Serialization;

/// <summary>
/// Атрибут для оптимизации массивов. 
/// Если в данных приходит пустой массив, подставляет Array.Empty без выделения памяти.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class OptimizeArrayEmptyAttribute(Type elementType) : MessagePackFormatterAttribute(typeof(ArrayEmptyOptimizationFormatter<>).MakeGenericType(elementType));

/// <summary>
/// Внутренний форматитер, который перехватывает пустые массивы
/// </summary>
public sealed class ArrayEmptyOptimizationFormatter<T> : IMessagePackFormatter<T[]?>
{
    public void Serialize(ref MessagePackWriter writer, T[]? value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(value.Length);
        var formatter = options.Resolver.GetFormatterWithVerify<T>();
        for (int i = 0; i < value.Length; i++)
        {
            formatter.Serialize(ref writer, value[i], options);
        }
    }

    public T[]? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.IsNil)
        {
            reader.ReadNil();
            return null;
        }

        int count = reader.ReadArrayHeader();

        //ОПТИМИЗАЦИЯ: Если массив пустой, возвращаем синглтон Array.Empty<T>()
        if (count == 0)
        {
            return Array.Empty<T>();
        }

        var array = new T[count];
        var formatter = options.Resolver.GetFormatterWithVerify<T>();
        for (int i = 0; i < count; i++)
        {
            array[i] = formatter.Deserialize(ref reader, options);
        }

        return array;
    }
}
