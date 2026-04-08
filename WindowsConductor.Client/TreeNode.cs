namespace WindowsConductor.Client;

public interface IReadOnlyTreeNode<out T>
{
    T Value { get; }
    IReadOnlyList<IReadOnlyTreeNode<T>> Children { get; }
}

public sealed class TreeNode<T>(T value) : IReadOnlyTreeNode<T>
{
    public T Value { get; } = value;
    private readonly List<TreeNode<T>> _children = [];
    public IReadOnlyList<IReadOnlyTreeNode<T>> Children => _children;

    public TreeNode<T> AddChild(T childValue)
    {
        var child = new TreeNode<T>(childValue);
        _children.Add(child);
        return child;
    }

    public void AddChild(TreeNode<T> child) => _children.Add(child);
}
