using System.Globalization;
using System.Numerics;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Server.DTOs;

namespace Server.Services;

// Validate the original files before creating a project or starting remote jobs.
public static class ProjectCreationValidation
{
  public static ApplicationService.ResponseService ValidatePredictionKind(IFormFile trainFile, StartProjectInDTO project)
  {
    var response = new ApplicationService.ResponseService();
    try
    {
      // The service infers the target again on TRAIN. In particular, a split can
      // change numeric cardinality relative to the number of observations.
      if (!project.SurvivalModel && !string.IsNullOrWhiteSpace(project.CreationContextJson))
      {
        using var context = JsonDocument.Parse(project.CreationContextJson);
        if (context.RootElement.TryGetProperty("predictionKind", out var kind) && kind.ValueKind == JsonValueKind.String
          && kind.GetString() is string requested && requested is "category" or "quantity")
        {
          var actual = InferPredictionKind(trainFile, project.CsvColumnToPredict);
          if (actual != requested)
          {
            var actualLabel = actual == "quantity" ? "une quantité" : "une catégorie";
            var requestedLabel = requested == "quantity" ? "une quantité" : "une catégorie";
            throw new ArgumentException($"DrIA interprète la cible du fichier d’apprentissage comme {actualLabel}, alors que vous avez choisi {requestedLabel}. Le découpage peut modifier cette détection. Choisissez la validation interne seule ou adaptez le codage de la cible, puis réessayez.");
          }
        }
      }
      response.SetSuccessful();
    }
    catch (Exception exception) when (exception is ArgumentException or JsonException or CsvHelperException or InvalidDataException)
    {
      response.HttpStatusCode = StatusCodes.Status400BadRequest;
      response.AddError(exception is CsvHelperException ? "La cible du fichier d’apprentissage ne peut pas être lue. Vérifiez le fichier CSV." : exception.Message);
    }
    return response;
  }

  private static string InferPredictionKind(IFormFile file, string target)
  {
    using var stream = file.OpenReadStream();
    using var reader = new StreamReader(stream);
    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, DetectDelimiter = true });
    if (!csv.Read() || !csv.ReadHeader() || !(csv.HeaderRecord ?? []).Contains(target, StringComparer.Ordinal))
      throw new ArgumentException("La colonne à prédire est absente du fichier d’apprentissage.");
    var numeric = true;
    var fractional = false;
    var nonFinite = false;
    var populatedCount = 0;
    var distinctNumbers = new HashSet<double>();
    while (csv.Read())
    {
      var raw = csv.GetField(target);
      if (string.IsNullOrWhiteSpace(raw)) continue;
      populatedCount++;
      if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
      {
        numeric = false; // Text and boolean target columns remain categorical.
        continue;
      }
      if (!double.IsFinite(number))
      {
        nonFinite = true;
        continue;
      }
      fractional |= number != Math.Truncate(number);
      // Above ten distinct values, the exact cardinality is no longer needed.
      if (distinctNumbers.Count <= 10) distinctNumbers.Add(number);
    }
    if (populatedCount == 0) throw new ArgumentException("La colonne à prédire ne contient aucune valeur renseignée.");
    if (numeric && nonFinite) throw new ArgumentException("La cible contient une valeur numérique non finie. Corrigez-la avant de lancer l’apprentissage.");
    return numeric && (fractional || distinctNumbers.Count > 10 || distinctNumbers.Count > populatedCount * 0.2) ? "quantity" : "category";
  }

  public static ApplicationService.ResponseService ValidateFiles(StartProjectInDTO project)
  {
    var response = new ApplicationService.ResponseService();
    try
    {
      var train = Inspect(project.File, project, null);
      if (project.EvaluationMode == "external")
      {
        if (project.TestFile == null) throw new ArgumentException("Importez le fichier réservé à l’évaluation.");
        var test = Inspect(project.TestFile, project, train.Headers);
        if (train.Groups.Overlaps(test.Groups))
          throw new ArgumentException("Certains patients apparaissent dans les deux fichiers. Réservez des patients différents pour l’apprentissage et l’évaluation.");
      }
      response.SetSuccessful();
    }
    catch (Exception exception) when (exception is ArgumentException or CsvHelperException or InvalidDataException)
    {
      response.HttpStatusCode = StatusCodes.Status400BadRequest;
      response.AddError(exception is CsvHelperException ? "Le fichier CSV contient une ligne mal formée. Vérifiez les séparateurs et le nombre de colonnes." : exception.Message);
    }
    return response;
  }

  private static (string[] Headers, HashSet<string> Groups) Inspect(IFormFile file, StartProjectInDTO project, string[]? expectedHeaders)
  {
    using var stream = file.OpenReadStream();
    using var reader = new StreamReader(stream);
    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, DetectDelimiter = true });
    if (!csv.Read() || !csv.ReadHeader()) throw new ArgumentException("Le fichier CSV ne contient pas d’en-tête.");
    var headers = csv.HeaderRecord ?? [];
    if (headers.Length == 0 || headers.Any(string.IsNullOrWhiteSpace) || headers.Distinct(StringComparer.Ordinal).Count() != headers.Length)
      throw new ArgumentException("Chaque colonne du fichier CSV doit avoir un nom unique et non vide.");
    if (expectedHeaders != null && !headers.ToHashSet(StringComparer.Ordinal).SetEquals(expectedHeaders))
      throw new ArgumentException("Le fichier d’évaluation doit contenir les mêmes colonnes que le fichier d’apprentissage.");

    var groups = project.RowDepedencies ? project.RowDepedenciesLinked : [];
    var excluded = project.DatasetHasIdColumn ? project.DatasetHasIdColumnList : [];
    var targets = project.SurvivalModel ? new[] { project.CsvColumnToPredict, project.SurvivalModelDatasetTargetTime! } : [project.CsvColumnToPredict];
    var missing = targets.Concat(groups).Concat(excluded).Except(headers, StringComparer.Ordinal).ToArray();
    if (missing.Length > 0) throw new ArgumentException($"Colonnes absentes du fichier CSV : {string.Join(", ", missing)}.");
    if (targets.Intersect(groups.Concat(excluded), StringComparer.Ordinal).Any())
      throw new ArgumentException("Une colonne à prédire ou de durée de suivi ne peut pas être exclue ni servir au regroupement des patients.");
    if (!headers.Except(targets.Concat(groups).Concat(excluded), StringComparer.Ordinal).Any())
      throw new ArgumentException("Conservez au moins une information pour réaliser la prédiction.");

    var groupValues = new HashSet<string>(StringComparer.Ordinal);
    var groupProfiles = groups.Select(_ => new GroupColumnProfile()).ToArray();
    var rowCount = 0;
    while (csv.Read())
    {
      rowCount++;
      if (csv.Parser.Count != headers.Length)
        throw new ArgumentException($"La ligne {rowCount + 1} du fichier CSV ne contient pas le nombre de colonnes attendu.");
      if (targets.Any(target => string.IsNullOrWhiteSpace(csv.GetField(target))))
        throw new ArgumentException($"Le résultat à prédire ou la durée de suivi est manquant à la ligne {rowCount + 1} du fichier CSV.");
      if (groups.Length > 0)
      {
        var values = groups.Select(group => csv.GetField(group) ?? string.Empty).ToArray();
        if (values.Any(string.IsNullOrWhiteSpace))
          throw new ArgumentException($"L’identifiant de regroupement est manquant à la ligne {rowCount + 1} du fichier CSV.");
        for (var index = 0; index < values.Length; index++) groupProfiles[index].Observe(values[index]);
        groupValues.Add(JsonSerializer.Serialize(values));
      }
    }
    if (rowCount == 0) throw new ArgumentException("Le fichier CSV ne contient aucune observation.");
    // pandas infers each complete column independently for each CSV. Only the
    // comparison keys are normalized; the original files remain untouched.
    var normalizedGroups = groupValues.Select(key =>
    {
      var values = JsonSerializer.Deserialize<string[]>(key)!;
      return JsonSerializer.Serialize(values.Select((value, index) => groupProfiles[index].ComparisonKey(value)).ToArray());
    }).ToHashSet(StringComparer.Ordinal);
    return (headers, normalizedGroups);
  }

  private sealed class GroupColumnProfile
  {
    private bool _numeric = true;
    private bool _boolean = true;
    private bool _signedInteger = true;
    private bool _unsignedInteger = true;
    private bool _lexicalInteger = true;

    public void Observe(string value)
    {
      _numeric &= double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number);
      _boolean &= value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase);
      _signedInteger &= long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
      _unsignedInteger &= ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
      _lexicalInteger &= BigInteger.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    public string ComparisonKey(string value)
    {
      if (_boolean) return value.Equals("true", StringComparison.OrdinalIgnoreCase) ? "number:1" : "number:0";
      // Preserve exact integer identifiers above the precision of a double.
      if (_signedInteger) return "number:" + long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
      if (_unsignedInteger) return "number:" + ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
      if (_numeric && !_lexicalInteger)
      {
        var number = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        return "number:" + (number == 0 ? "0" : number.ToString(number == Math.Truncate(number) ? "0" : "R", CultureInfo.InvariantCulture));
      }
      // Mixed or out-of-range integer columns remain text, as with read_csv.
      return "text:" + value;
    }
  }
}
