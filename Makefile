.PHONY: build test format clean

build:
	dotnet build

test:
	dotnet test

format:
	dotnet format

clean:
	dotnet clean
	rm -rf publish/
