{ pkgs ? import <nixpkgs> {} }:

pkgs.mkShell {
  buildInputs = [ pkgs.openjdk pkgs.protobuf pkgs.maven ];
}
