using AntiFraud.Core.BallTree.Entities;

using AntiFraud.Core.NeighborhoodClassifier.Services;

using AntiFraud.Core.VectorizedReference.Entities;



namespace AntiFraud.Application.NeighborhoodClassifier.Services;



public sealed class BallTreeNeighborhoodClassifier : INeighborhoodClassifier

{

    private readonly IBallTreeDataSource _dataSource;

    private readonly int _leafSize;

    private BallTreeEntity? _tree;



    /// <summary>Callback opcional para acompanhar progresso da construção em tempo real.</summary>

    public Action<int, int>? BuildProgressCallback { get; set; }



    /// <summary>Diagnóstico: tempo por fase nos primeiros níveis (depth, fase, ms).</summary>

    public Action<int, string, long>? BuildPhaseCallback { get; set; }



    /// <summary>Instância atual (após <see cref="Initialize"/> ou <see cref="AttachTree"/>).</summary>

    public BallTreeEntity? Tree => _tree;



    public BallTreeNeighborhoodClassifier(IBallTreeDataSource dataSource, int leafSize = VectorDatasetConstants.BallTreeLeafSize)

    {

        _dataSource = dataSource;

        _leafSize = leafSize;

    }



    /// <summary>Usa árvore restaurada do cache em disco (startup rápido).</summary>

    public void AttachTree(BallTreeEntity tree)

    {

        _tree = tree ?? throw new ArgumentNullException(nameof(tree));

    }



    public void Initialize()

    {

        _tree = new BallTreeEntity(_dataSource, _leafSize)

        {

            ProgressCallback = BuildProgressCallback,

            PhaseCallback = BuildPhaseCallback,

        };

    }



    public (int FraudCount, int Total) GetNeighborVote(ReadOnlySpan<float> queryVector, int k)

    {

        if (_tree is null)

            throw new InvalidOperationException("BallTree has not been initialized. Call Initialize() first.");



        var fraud = _tree.CountFraudAmongKNearest(queryVector, k, out var total);

        return (fraud, total);

    }

}


