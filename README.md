# GenCode

Generate C# from Misskey api.json

## Dependedencies Library

* System.Text.Json

## Supported Environment

* Net6.0+

## Current Problem

* This generator is NOT support all endpoints
* And some endpoints cannot be generate code
* Some Enums and Properties are using specified letters(like +,-,.), This generator is replaceing these letter to Word(Like + to Plus) or removing these letters
