{ pkgs ? import <nixpkgs> {} }:

pkgs.mkShell {
  buildInputs = [
    pkgs.dotnet-sdk_8  # or another version as required
    pkgs.dotnetPackages.Nuget
  ];

  DOTNET_ROOT = "${pkgs.dotnet-sdk_8}";
}
