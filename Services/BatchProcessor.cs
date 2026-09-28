using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SilentLens.Core;
using SilentLens.Models;

namespace SilentLens.Services;

public sealed class BatchProgressInfo
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
}

public static class BatchProcessor
{
    /// <summary>
    /// Асинхронно обработать список файлов. Возвращает агрегированный отчёт.
    /// </summary>
    public static async Task<BatchResult> ProcessAsync(
        IList<PhotoItem> items,
        WipeMode mode,
        bool saveBackup,
        IProgress<BatchProgressInfo> progress,
        CancellationToken token)
    {
        var result = new BatchResult { Total = items.Count };

        await Task.Run(() =>
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    result.Cancelled = true;
                    break;
                }

                var item = items[i];

                progress?.Report(new BatchProgressInfo
                {
                    Current = i + 1,
                    Total = items.Count,
                    CurrentFile = item.FileName
                });

                var itemResult = new BatchItemResult
                {
                    FilePath = item.FilePath,
                    FileName = item.FileName
                };

                try
                {
                    if (item.IsReadOnly)
                    {
                        itemResult.Status = "Skipped";
                        itemResult.Message = $"Формат {item.Extension} только для чтения";
                        result.Skipped++;
                    }
                    else
                    {
                        var r = WiperService.WipeFile(item.FilePath, mode, saveBackup);

                        if (r.Success)
                        {
                            itemResult.Status = "OK";
                            itemResult.SavedBytes = r.SavedBytes;
                            itemResult.BackupPath = r.OriginalBackupPath;
                            result.Processed++;
                            result.SavedBytes += r.SavedBytes;
                        }
                        else
                        {
                            itemResult.Status = "Failed";
                            itemResult.Message = r.Error;
                            result.Failed++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    itemResult.Status = "Failed";
                    itemResult.Message = ex.Message;
                    result.Failed++;
                }

                result.Items.Add(itemResult);
            }
        }, token);

        return result;
    }
}