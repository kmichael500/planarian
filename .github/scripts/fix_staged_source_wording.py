from pathlib import Path

path = Path("Planarian/Planarian/Modules/Files/Services/FileService.cs")
text = path.read_text()
old = '''        catch
        {
            // Never remove the staged source on failure. Only compensate the
            // deterministic destination created for this publication attempt.
            await BestEffortDeleteBlobAsync(blobKey, RequestUser.AccountContainerName);
            throw;
        }'''
new = '''        catch
        {
            // Compensate only the deterministic destination created by this
            // publication attempt. The staged source belongs to the upload-
            // session lifecycle and is cleaned by its caller.
            await BestEffortDeleteBlobAsync(blobKey, RequestUser.AccountContainerName);
            throw;
        }'''
if text.count(old) != 1:
    raise SystemExit(f"expected one staged-source compensation comment, found {text.count(old)}")
path.write_text(text.replace(old, new, 1))
