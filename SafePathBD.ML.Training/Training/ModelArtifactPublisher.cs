using SafePathBD.ML.Training.Models;

namespace SafePathBD.ML.Training.Training;

public static class ModelArtifactPublisher
{
    public static void PublishSelected(ModelEvaluationResult selected, string canonicalPath)
    {
        if (string.IsNullOrWhiteSpace(selected.ArtifactPath) || !File.Exists(selected.ArtifactPath))
            throw new FileNotFoundException("Selected model artifact does not exist.", selected.ArtifactPath);
        Directory.CreateDirectory(Path.GetDirectoryName(canonicalPath)!);
        File.Copy(selected.ArtifactPath, canonicalPath, overwrite: true);
    }
}
