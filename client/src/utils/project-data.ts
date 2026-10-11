import { parse, Parser, ParseResult } from 'papaparse';

export type ProjectScalar = string | number | boolean;
export type ColumnKind = 'numeric' | 'boolean' | 'text' | 'empty';
export type ProjectColumnRole = 'target' | 'event' | 'time' | 'group' | 'identifier' | 'unavailable' | 'predictor';
export interface ProjectColumnAnnotation {
  role: ProjectColumnRole;
  label: string;
  reason?: string;
}

/** Profiles cover the entire file. Empty and whitespace-only cells are missing. */
export interface ColumnProfile {
  type: ColumnKind;
  missingCount: number;
  populatedCount: number;
  nonFiniteCount: number;
  /** At most 100 distinct values; truncated lists must never be treated as exhaustive. */
  distinctValues: ProjectScalar[];
  valueCounts: { value: ProjectScalar; count: number }[];
  distinctValuesTruncated: boolean;
  min?: number;
  max?: number;
}

export interface ProjectDataset {
  columns: string[];
  /** Original strings from the first 100 observations, without transformations. */
  rows: Record<string, string>[];
  rowCount: number;
  profiles: Record<string, ColumnProfile>;
  /** During onPreview, counts and profiles describe only the portion read so far. */
  isComplete: boolean;
}

export interface ReadProjectCsvOptions {
  signal?: AbortSignal;
  /** First preview is emitted before profiling the rest of a large file. */
  onPreview?: (dataset: ProjectDataset) => void;
}

const MAX_PREVIEW_ROWS = 100;
const MAX_DISTINCT_VALUES = 100;
const NUMERIC = /^[+-]?(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?$/i;
const BOOLEAN = /^(true|false)$/i;
const NON_FINITE = /^[+-]?(?:infinity|nan)$/i;

interface ColumnAccumulator {
  missingCount: number;
  populatedCount: number;
  nonFiniteCount: number;
  allNumeric: boolean;
  allBoolean: boolean;
  counts: Map<string, number>;
  truncated: boolean;
  min: number;
  max: number;
}

function accumulator(): ColumnAccumulator {
  return {
    missingCount: 0,
    populatedCount: 0,
    nonFiniteCount: 0,
    allNumeric: true,
    allBoolean: true,
    counts: new Map(),
    truncated: false,
    min: Infinity,
    max: -Infinity,
  };
}

/** Same column-wide conversion rules as inferCsvColumnValues. */
export function projectCsvScalar(value: string, profile: Pick<ColumnProfile, 'type'>): ProjectScalar {
  if (profile.type === 'numeric') return Number(value);
  if (profile.type === 'boolean') return value.toLowerCase() === 'true';
  return value;
}

function profileColumn(column: ColumnAccumulator): ColumnProfile {
  const type: ColumnKind = column.populatedCount === 0 ? 'empty'
    : column.allNumeric ? 'numeric' : column.allBoolean ? 'boolean' : 'text';
  const counts = new Map<ProjectScalar, number>();
  column.counts.forEach((count, rawValue) => {
    const value = projectCsvScalar(rawValue, { type });
    counts.set(value, (counts.get(value) ?? 0) + count);
  });
  return {
    type,
    missingCount: column.missingCount,
    populatedCount: column.populatedCount,
    nonFiniteCount: column.nonFiniteCount,
    distinctValues: [...counts.keys()],
    valueCounts: [...counts].map(([value, count]) => ({ value, count })),
    distinctValuesTruncated: column.truncated,
    ...(type === 'numeric' ? { min: column.min, max: column.max } : {}),
  };
}

/**
 * Stream the full CSV in a worker, retaining a bounded preview and value catalogue.
 * Headers are read as a raw row, so Papa cannot silently rename duplicate names.
 * No trimming or type conversion is applied to the observations shown in the table.
 */
export function readProjectCsv(file: File, options: ReadProjectCsvOptions = {}): Promise<ProjectDataset> {
  return new Promise((resolve, reject) => {
    let columns: string[] = [];
    const rows: Record<string, string>[] = [];
    let rowCount = 0;
    let accumulators: ColumnAccumulator[] = [];
    let parser: Parser | undefined;
    let settled = false;
    let previewEmitted = false;

    const snapshot = (isComplete: boolean): ProjectDataset => ({
      columns: [...columns],
      rows: [...rows],
      rowCount,
      profiles: Object.fromEntries(columns.map((name, index) => [name, profileColumn(accumulators[index])])),
      isComplete,
    });

    const fail = (error: Error) => {
      if (settled) return;
      settled = true;
      options.signal?.removeEventListener('abort', abort);
      reject(error);
      parser?.abort();
    };
    const abort = () => fail(new DOMException('Lecture du fichier annulée.', 'AbortError'));
    if (options.signal?.aborted) {
      abort();
      return;
    }
    options.signal?.addEventListener('abort', abort, { once: true });

    try {
      parse<string[]>(file, {
        // Reading the header ourselves also catches duplicates in Papa 5.4,
        // whose metadata does not yet report renamedHeaders.
        header: false,
        worker: true,
        dynamicTyping: false,
        skipEmptyLines: 'greedy',
        delimitersToGuess: [',', ';', '\t', '|'],
        chunkSize: 256 * 1024,
        chunk: (result: ParseResult<string[]>, handle: Parser) => {
          parser = handle;
          if (settled || options.signal?.aborted) {
            handle.abort();
            return;
          }
          const renamedHeaders = (result.meta as typeof result.meta & { renamedHeaders?: Record<string, string> }).renamedHeaders;
          if (renamedHeaders && Object.keys(renamedHeaders).length) {
            fail(new Error('Les noms de colonnes doivent être uniques. Renommez les colonnes en double dans votre fichier.'));
            return;
          }
          // Papa cannot guess a delimiter in a valid one-column CSV.
          const errors = result.errors.filter((error) => error.code !== 'UndetectableDelimiter');
          if (errors.length) {
            fail(new Error(`Lecture du fichier : ${errors[0].message}`));
            return;
          }
          for (const cells of result.data) {
            if (!columns.length) {
              columns = cells.map((name, index) => index === 0 ? name.replace(/^\uFEFF/, '') : name);
              const normalizedNames = columns.map((name) => name.trim());
              if (!columns.length || normalizedNames.some((name) => !name)
                || new Set(normalizedNames).size !== columns.length) {
                fail(new Error('Les colonnes doivent avoir des noms uniques et non vides. Vérifiez la première ligne du fichier.'));
                return;
              }
              accumulators = columns.map(accumulator);
              continue;
            }
            if (cells.length !== columns.length) {
              fail(new Error(`L’observation ${rowCount + 1} contient ${cells.length} cellules au lieu de ${columns.length}. Vérifiez les séparateurs du fichier.`));
              return;
            }
            rowCount += 1;
            if (rows.length < MAX_PREVIEW_ROWS) {
              rows.push(Object.fromEntries(columns.map((column, index) => [column, cells[index]])));
            }
            cells.forEach((value, index) => {
              const column = accumulators[index];
              const trimmed = value.trim();
              if (!trimmed) {
                column.missingCount += 1;
                return;
              }
              column.populatedCount += 1;
              const finiteNumeric = NUMERIC.test(trimmed) && Number.isFinite(Number(value));
              column.allNumeric = column.allNumeric && finiteNumeric;
              column.allBoolean = column.allBoolean && BOOLEAN.test(value);
              if (NON_FINITE.test(trimmed) || (NUMERIC.test(trimmed) && !finiteNumeric)) column.nonFiniteCount += 1;
              if (finiteNumeric) {
                column.min = Math.min(column.min, Number(value));
                column.max = Math.max(column.max, Number(value));
              }
              if (column.counts.has(value)) column.counts.set(value, (column.counts.get(value) ?? 0) + 1);
              else if (column.counts.size < MAX_DISTINCT_VALUES) column.counts.set(value, 1);
              else column.truncated = true;
            });
          }
          if (!previewEmitted && rows.length && options.onPreview) {
            previewEmitted = true;
            options.onPreview(snapshot(false));
          }
        },
        complete: () => {
          if (settled) return;
          if (!columns.length || !rowCount) {
            fail(new Error('Le fichier doit contenir une ligne de noms de colonnes et au moins une observation.'));
            return;
          }
          settled = true;
          options.signal?.removeEventListener('abort', abort);
          resolve(snapshot(true));
        },
        error: (error: Error) => fail(new Error(`Impossible de lire le fichier : ${error.message}`)),
      });
    } catch (error) {
      fail(error instanceof Error ? error : new Error('Impossible de lire ce fichier CSV.'));
    }
  });
}
