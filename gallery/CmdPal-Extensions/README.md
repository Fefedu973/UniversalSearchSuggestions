# Command Palette Gallery submission

This folder mirrors the structure expected by `microsoft/CmdPal-Extensions`.

To submit:

```powershell
git clone https://github.com/<your-fork>/CmdPal-Extensions.git
Copy-Item -Recurse .\gallery\CmdPal-Extensions\extensions\fefedu973 .\CmdPal-Extensions\extensions\
cd .\CmdPal-Extensions
git checkout -b add-universal-search-suggestions
git add extensions\fefedu973\universal-search-suggestions
git commit -m "Add fefedu973.universal-search-suggestions to gallery"
git push origin add-universal-search-suggestions
```

Then open a pull request against `microsoft/CmdPal-Extensions/main`.

The `installSources[0].uri` currently points to the existing public releases page. Before opening the PR, make sure that release contains the Command Palette package, not only the older PowerToys Run plugin package.
