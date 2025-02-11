using System;
using MathNet.Numerics.LinearAlgebra.Single;
using MathNet.Numerics.Distributions;

// Alias for brevity.
using MatrixF = MathNet.Numerics.LinearAlgebra.Matrix<float>;
using VectorF = MathNet.Numerics.LinearAlgebra.Vector<float>;

public class SelfAttention
{
    // The dimensionality of the model (and of the query, key, value vectors).
    public int ModelDim { get; private set; }

    // Weight matrix for computing queries. Shape: (ModelDim, ModelDim)
    public MatrixF W_q { get; private set; }

    // Weight matrix for computing keys. Shape: (ModelDim, ModelDim)
    public MatrixF W_k { get; private set; }

    // Weight matrix for computing values. Shape: (ModelDim, ModelDim)
    public MatrixF W_v { get; private set; }

    // Output projection weight matrix. Shape: (ModelDim, ModelDim)
    public MatrixF W_o { get; private set; }

    // --------------------------------------------------------------------------------------------

    // Creates a new self-attention layer with the given model dimensionality.
    // modelDim: The size of the model and each token’s embedding vector.</param>
    public SelfAttention(int modelDim)
    {
        ModelDim = modelDim;

        // Create a uniform distribution for small random initialization.
        float rangeMin = -1f;
        float rangeMax =  1f;
        SetRandomWeights(rangeMin, rangeMax);
    }

    // --------------------------------------------------------------------------------------------

    public void SetRandomWeights(float min, float max)
    {
        // Create a uniform distribution for small random initialization.
        float rangeMin = min;
        float rangeMax = max;

        // Initialize weight matrices.
        W_q = DenseMatrix.Build.Random(ModelDim, ModelDim, new ContinuousUniform(rangeMin, rangeMax));
        W_k = DenseMatrix.Build.Random(ModelDim, ModelDim, new ContinuousUniform(rangeMin, rangeMax));
        W_v = DenseMatrix.Build.Random(ModelDim, ModelDim, new ContinuousUniform(rangeMin, rangeMax));
        W_o = DenseMatrix.Build.Random(ModelDim, ModelDim, new ContinuousUniform(rangeMin, rangeMax));
    }

    public void ApplyRandomOffset(float absOffset)
    {
        // The offset for each value is plus/minus a random value in the range [-absOffset, absOffset].
        // A different offset is applied to each value in each matrix.
        float rangeMin = -absOffset;
        float rangeMax = absOffset;

        // Initialize weight matrices.
        MatrixF offsetq = DenseMatrix.Build.Random(ModelDim, ModelDim, new ContinuousUniform(rangeMin, rangeMax));
        MatrixF offsetk = DenseMatrix.Build.Random(ModelDim, ModelDim, new ContinuousUniform(rangeMin, rangeMax));
        MatrixF offsetv = DenseMatrix.Build.Random(ModelDim, ModelDim, new ContinuousUniform(rangeMin, rangeMax));
        MatrixF offseto = DenseMatrix.Build.Random(ModelDim, ModelDim, new ContinuousUniform(rangeMin, rangeMax));

        // Apply the offset to each element in each matrix.
        W_q += offsetq;
        W_k += offsetk;
        W_v += offsetv;
        W_o += offseto;

        NormalizeWeights();
    }

    public void NormalizeWeights()
    {
        W_q.TanhNormalize();
        W_k.TanhNormalize();
        W_v.TanhNormalize();
        W_o.TanhNormalize();
    }

    // --------------------------------------------------------------------------------------------

    public int ParamCount()
    {
        return W_q.RowCount * W_q.ColumnCount +
               W_k.RowCount * W_k.ColumnCount +
               W_v.RowCount * W_v.ColumnCount +
               W_o.RowCount * W_o.ColumnCount;
    }

    // --------------------------------------------------------------------------------------------

    // Applies the self-attention mechanism to the given input.
    // The input matrix with shape (sequenceLength, ModelDim) where each row is an embedded token.
    // The output matrix after applying self-attention. Its shape is (sequenceLength, ModelDim).
    public MatrixF Forward(MatrixF input)
    {
        // Compute queries, keys, and values.
        // If input has shape (n, ModelDim) and W_x has shape (ModelDim, ModelDim), then the result is (n, ModelDim).
        MatrixF Q = input * W_q;
        MatrixF K = input * W_k;
        MatrixF V = input * W_v;

        // Compute raw attention scores with Q * K^T.
        // This gives a score matrix of shape (n, n).
        MatrixF scores = Q * K.Transpose();

        // Scale the scores by 1/sqrt(ModelDim) for numerical stability.
        float scale = 1.0f / MathF.Sqrt(ModelDim);
        scores = scores.Multiply(scale);

        // Apply softmax to each row to obtain attention weights.
        MatrixF attentionWeights = SoftmaxRows(scores);

        // Multiply the attention weights by V to get the context (weighted sum).
        MatrixF context = attentionWeights * V;

        // Apply a final linear projection.
        MatrixF output = context * W_o;

        return output;
    }

    // --------------------------------------------------------------------------------------------

    // Applies the softmax function to each row of the given matrix.
    private MatrixF SoftmaxRows(MatrixF matrix)
    {
        // Clone the input to avoid modifying the original matrix.
        MatrixF result = matrix.Clone();

        for (int i = 0; i < matrix.RowCount; i++)
        {
            // Get the i-th row.
            VectorF row = matrix.Row(i);

            // For numerical stability, subtract the row maximum.
            float max = row.Maximum();
            VectorF expRow = row.Subtract(max).Map(x => MathF.Exp(x));

            // Sum of exponentials.
            float sum = expRow.Sum();

            // Normalize to form the softmax distribution.
            VectorF softmaxRow = expRow.Divide(sum);

            // Copy the normalized values back into the result matrix.
            for (int j = 0; j < matrix.ColumnCount; j++)
            {
                result[i, j] = softmaxRow[j];
            }
        }

        return result;
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Load Save
    // --------------------------------------------------------------------------------------------

    // Save the self-attention layer to a file.
    public void SaveToFile(string path)
    {
        using (var writer = new StreamWriter(path))
        {
            // Write the model dimension.
            writer.WriteLine(ModelDim);

            // Save each weight matrix.
            SaveMatrix(writer, W_q);
            SaveMatrix(writer, W_k);
            SaveMatrix(writer, W_v);
            SaveMatrix(writer, W_o);
        }
    }

    // --------------------------------------------------------------------------------------------

    // Helper method to save a matrix.
    private static void SaveMatrix(StreamWriter writer, MatrixF matrix)
    {
        // Write matrix dimensions (rows and columns).
        writer.WriteLine($"{matrix.RowCount} {matrix.ColumnCount}");

        // Write each row as a space-separated list of floats.
        for (int i = 0; i < matrix.RowCount; i++)
        {
            writer.WriteLine(string.Join(" ", matrix.Row(i).ToArray()));
        }
    }

    // --------------------------------------------------------------------------------------------

    // Load a self-attention layer from a file.
    public static SelfAttention LoadFromFile(string path)
    {
        using (var reader = new StreamReader(path))
        {
            // Read the model dimension.
            int modelDim = int.Parse(reader.ReadLine());

            // Create a new SelfAttention instance.
            // (The constructor will initialize random weights, but we overwrite them below.)
            SelfAttention layer = new SelfAttention(modelDim);

            // Load each weight matrix and assign to the corresponding property.
            layer.W_q = MatrixOperations.LoadMatrix(reader);
            layer.W_k = MatrixOperations.LoadMatrix(reader);
            layer.W_v = MatrixOperations.LoadMatrix(reader);
            layer.W_o = MatrixOperations.LoadMatrix(reader);

            return layer;
        }
    }
}
