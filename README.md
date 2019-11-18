
# Linky

Command line tool for scanning a website for broken links. Inspired by [Linkinator](https://github.com/JustinBeckwith/linkinator).

## Installation

Clone the repo to you computer. Pull to get updates. Open a command prompt to the source directory.

If you are updating, remove the old version first:

```cmd
> dotnet tool uninstall -g linky
```

To install:

```cmd
> dotnet build
> dotnet pack
> dotnet tooll install -g --add-source ./dist linky
```

## Usage

```cmd
> linky URL
```

You call Linky with a URL, which should include `http` or `https` at the beginning. Trailing slashes are fine.

### Options

- `-r` or `--recursive` - If Linky encounters a link with the same root URL as the argument URL, then Linky will download and parse that URL as well. It will skip URLs that have already been scanned.

