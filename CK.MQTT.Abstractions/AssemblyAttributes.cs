using System;

[assembly: Fody.ConfigureAwait( false )]

[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method )]
public class ThreadColorAttribute : Attribute
{
    public ThreadColorAttribute( string colorName )
    {
        ColorName = colorName;
        Color = ThreadColor.Special;
    }

    public ThreadColorAttribute( ThreadColor color )
    {
        Color = color;
    }

    public string? ColorName { get; }
    public ThreadColor Color { get; }
}

public enum ThreadColor
{
    None,
    Special,
    Rainbow
}
