# AppConfigEncodingSpike

Demo für die Implementierung eines ```ProtectedConfigurationProvider``` um Teile (Sektionen) einer ```app.config``` oder ```web.config``` zur ver- und entschlüsseln. 

**Achtung** 

Die gezeigte Implementierung dient zur Demonstration. Für den produktiven Einsatz ist sie nur bedingt geeignet. Zwar funktioniert sie - und die Verschlüsselung selber ist sehr sicher. Aber das Geheimnis (sprich Passwort) kann praktisch im Klartext in der Konfigurationsdatei abgelesen werden!



[TOC]

## Funktionsweise

### Grundsätzliches

Auf Anwendungsebene besteht zur Laufzeit stets transparent Zugriff auf die Klartextinhalte der jeweiligen Sektionen. - Unabhängig davon, ob die entsprechende Sektion in der Konfigurationsdatei zum jeweiligen Zeitpunkt ver- oder entschlüsselt ist. Voraussetzung dafür ist, dass in der Anwendung stets via ```System.Configuration.ConfigurationManager``` auf die Konfiguration zugegriffen wird.

Bei einem direkten Zugriff auf die Konfigurationsdatei (z.B. Einlesen als XML Dokument) müssen die Sektionen ggf. manuell entschlüsselt werden. Dies geschieht dann nicht on the fly.

### Implementierung

#### ProtectedConfigurationProvider

Für eine spezifische Implementierung des Providers muss die vom .NET Framework bereitgestellte abstrakten Basisklasse ```ProtectedConfigurationProvider``` implementiert werden.  - Es müssen Überschreibungen für die folgenden abstrakten Methoden bereitgestellt werden:

* ```Initialize()```

  Wird von der Konfigurationsinfrastruktur des .NET Frameworks aufgerufen, um eine Instanz des Provider zu initialisieren. Die Methode bekommt den Namen des Providers sowie eine Sammlung von Name/Wert-Paaren hereingereicht, die statisch in der Konfiguration [der Anwendung] angegeben wurden (siehe dazu weiter unten - die Beschreibung der Konfiguration).

  Die Methode wird 1x bei der Initialisierung aufgerufen und muss alle notwendigen Aktionen ausführen, damit die Instanz in der Folge XML Knoten ver- oder entschlüsseln kann.

* ```Encrypt()```

  Wird von der Konfigurationsinfrastruktur des .NET Frameworks aufgerufen, um einen XML Knoten [einer Konfigurationsdatei] zu verschlüsseln. Der Provider kapselt hier also die eigentliche Verschlüsselungsmethode.

  Die Infrastruktur reicht den gesamten zu verschlüsselnden XML Knoten herein und erwartet, dass dessen Inhalt vollständig verschlüsselt wird (im Falle der ```ConnectionsStrings``` Sektion wäre das der  ```<connectionStrings>``` Knoten). Demzufolge muss über ```OuterXml``` das Markup des Knotens selber sowie aller untergeordneter Knoten abgerufen werden.

  Die Konfigurationsinfrastruktur des .NET Frameworks erwartet als Ausgang einen einzelnen Knoten namens ```<EncryptedData>```, der als Inhalt die verschlüsselten Inhalte trägt. In der Regel in Form einer kodierten Zeichenfolge (z.B. im Base64 Format). Die Details hängen aber von der Implementierung des jeweiligen Providers ab.

* ```Decrypt()```

  Wird von der Konfigurationsinfrastruktur des .NET Frameworks aufgerufen, um einen XML Knoten [einer Konfigurationsdatei] mit verschlüsselten Inhalt zu entschlüsseln. Der Provider kapselt hier also die eigentliche Entschlüsselungsmethode.

  Die Infrastruktur hat den benötigten Provider identifiziert - und die Notwendigkeit zur Entschlüsselung sichergestellt. Hereingereicht wird der ```<EncryptedData>``` XML Knoten, der beim Aufruf von ```Encrypt()``` erstellt worden ist. Der Zugriff erfolgt also via ```InnerXml``` auf den [verschlüsselten] Inhalt des Knotens.

  Die Konfigurationsinfrastruktur des .NET Frameworks erwartet als Ausgang einen einzelnen Knoten in derselben Form, die bei ```Encrypt()``` als Eingangsparameter übergeben wurde (also quasi die Restaurierung des XML Knotens im Klartext).

#### SymmetricProtectedConfigurationProvider

Die Klasse ```SymmetricProtectedConfigurationProvider``` (im gleichnamigen Unterprojekt) implementiert den benötigten Provider. Sie wird dafür von der im Framework bereitgestellten abstrakten Basisklasse ```ProtectedConfigurationProvider``` abgeleitet.

Die eigentliche Verschlüsselung erfolgt via **AES**. Das notwendige Passwort (und die Angabe für das Salt) werden extern festgelegt. Sie werden über die Name/Wert Sammlung, die der ```Initialize()``` Methode übergeben werden, transportiert:

| Key  | Inhalt                                                       |
| ---- | ------------------------------------------------------------ |
| seed | Stammwert für die Ableitung des Schlüssels. Im Prinzip das Passwort. |
| salt | Salt für die Ableitung des Schlüssels.<br />Die Angabe erfolgt entweder als Hexstring (mit oder ohne Präfix $, 0x oder &h) - Groß-/Kleinschreibung spielt keine Rolle - oder als Base64 kodierter String. Schlägt auch die Base64 Dekodierung fehl, dann wird das Salt als UTF8 Zeichenkette aufgefasst und direkt in die binäre Entsprechung (Byte Array) umgesetzt.<br />Wichtig: Das Salt ist Prinzipiell optional - aber in dieser Implementierung zwingend anzugeben; **und es muss mindesten 8 Bytes lang sein.** |

Alle weiteren Keys in der Sammlung werden ignoriert. Ist einer der beiden benötigten Keys nicht vorhanden oder der Wert leer, dann erfolgt eine Exception.

Diese Name/Wert-Sammlung wird von der Konfigurationsinfrastruktur des .NET Frameworks aus der Deklaration des Providers in der Konfigurationsdatei (```app.config```)  befüllt - siehe weiter unten.

Die Angaben für ```seed``` und ```salt``` dienen als Eingangsparameter für eine KeyDerivation. Diese liefert dann den eigentlichen Key und den Initialisierungsvektor für die AES Verschlüsselung.

Die Assembly erhält einen StrongName, damit die Konfigurationsinfrastruktur des .NET Frameworks die Deklaration des Providers (s.u.) akzeptiert.

  ## Konfiguration

  ### ProtectedConfigurationProvider konfigurieren

Damit ```ProtectedConfigurationProvider``` verwendet werden können, müssen sie deklariert werden. Für die ```ProtectedConfigurationProvider``` aus dem Lieferumfang des .NET Framework (das sind [System.Configuration.DpapiProtectedConfigurationProvider](https://learn.microsoft.com/de-de/dotnet/api/system.configuration.dpapiprotectedconfigurationprovider?view=dotnet-plat-ext-7.0) und [System.Configuration.RsaProtectedConfigurationProvider](https://learn.microsoft.com/de-de/dotnet/api/system.configuration.rsaprotectedconfigurationprovider?view=dotnet-plat-ext-7.0)) erfolgt das bereits in der ```machine.config```, so dass sie out of the box zur Verfügung stehen.

Für anwendungsspezifische ```ProtectedConfigurationProvider```  erfolgt dies in der ```app.config```/```web.config``` der betreffenden Anwendung - im Abschnitt ```configProtectedData```:

Der Eintrag erhält einen Namen, unter dem der jeweilige Provider 

* in der Konfigurationsdatei referenziert werden kann

  Werden Konfigurationsabschnitte (Sektionen) verschlüsselt, dann werden sie durch eine Knotenstruktur ersetzt, in der u.a. der verwendete Provider genannt wird.

* im Anwendungscode referenziert werden kann

  Zumindest die Verschlüsselung einer Sektion muss anwendungsseitig (in derselben Anwendung - oder einem externen Tool) erfolgen; dabei muss der exakte Bezeichner verwendet werden, der bei der Deklaration des Providers in der Konfiguration der Zielanwendung angegeben wurde/wird.

Zwingend muss dann der Typ des Providers angegeben werden - inkl. Nennung der Assembly und des Public Key Tokens (d.h. vollständiger Typname). **Alle weiteren Attribute** werden der ```Initialize()```Methode des Providers über die Name/Wert-Sammlung bereitgestellt.

**Beispiel:** Deklaration des SymmetricProtectedConfigurationProvider

```xml
  <configProtectedData>
    <providers>
      <add name="SymmetricProtectedConfigurationProvider"
        type="de.Aargenveldt.Auxiliary.Configuration.Encoding.ConfigurationProviders.SymmetricProtectedConfigurationProvider, SymmetricProtectedConfigurationProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=63b973b832c470c2, processorArchitecture=MSIL"
        seed="AKeySeed"
        salt="QUtleVNhbHQ=" />
    </providers>
  </configProtectedData>
```

Als Name wurde ```SymmetricProtectedConfigurationProvider``` gewählt - und die Typangabe entspricht der ```SymmetricProtectedConfigurationProvider``` Klasse aus dem gleichnamigen Unterprojekt.

Die weiteren Attribute (```seed``` und ```salt```) werden dann der ```Initialize()``` Methode übermittelt.

 

### Anwendungskonfigurationen

#### Ausgangskonfiguration

Die Ausgangskonfiguration im Klartext umfasst neben der Deklaration des ```SymmetricProtectedConfigurationProvider``` eine ```ConnectionStrings``` Sektion. Letztere dient der Demonstration: Diese Sektion wird auf Benutzerwunsch ver- oder entschlüsselt. (Details siehe bei der Beschreibung der Nutzung weiter unten).

  ```xml
  <?xml version="1.0" encoding="utf-8" ?>
  <configuration>
    <configProtectedData>
      <providers>
        <add name="SymmetricProtectedConfigurationProvider"
          type="de.Aargenveldt.Auxiliary.Configuration.Encoding.ConfigurationProviders.SymmetricProtectedConfigurationProvider, SymmetricProtectedConfigurationProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=63b973b832c470c2, processorArchitecture=MSIL"
          seed="AKeySeed"
          salt="QUtleVNhbHQ=" />
      </providers>
    </configProtectedData>
  
  
    <connectionStrings>
      <clear/>
      <add name="foo" connectionString="This is some foo plaintext connectionstring"/>
      <add name="bar" connectionString="This is some bar plaintext connectionstring"/>
    </connectionStrings>
  
  
    <startup>
      <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.7.2" />
    </startup>
  </configuration>
  ```

  

#### Verschlüsselte Konfiguration

Beispiel für eine verschlüsselte ```ConnectionStrings``` Sektion:

Der ```connectionStrings``` Knoten wurden um das Attribut ```configProtectionProvider``` ergänzt. Dieses Attribut stellt an Hand des Bezeichners die Beziehung zum verwendeten ProtectedConfigurationProvider her. Der Inhalt des ```configProtectionProvider``` wurde verschlüsselt und im ```EncryptedData``` Unterknoten abgelegt.

  ```xml
  <?xml version="1.0" encoding="utf-8" ?>
  <configuration>
    <configProtectedData>
      <providers>
        <add name="SymmetricProtectedConfigurationProvider"
          type="de.Aargenveldt.Auxiliary.Configuration.Encoding.ConfigurationProviders.SymmetricProtectedConfigurationProvider, SymmetricProtectedConfigurationProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=63b973b832c470c2, processorArchitecture=MSIL"
          seed="AKeySeed"
          salt="QUtleVNhbHQ=" />
      </providers>
    </configProtectedData>
  
  
    <connectionStrings configProtectionProvider="SymmetricProtectedConfigurationProvider">
      <EncryptedData>tKCw1tWiCgNuuxglS/+m5tVjaUE63hZ4RflpiCSo/NI1UeZ8KAE0NWhZLtgCXE3ZrqiaYlZWQ/kopN9eE0KhCWVoZoc+HO3VE+Pr5BAH/SLyVRNvL0gNWac9EqCITcdQDRl8jtHFpl/hOOdPGl+SMSGytfojPVf4jKz5u+ApNhHxIKNLpy/3eLulz/5uWsXEfwwhM780hcxZClSFWcSf2m2VwuCOiO1LJhiAXpq3LKycOSA9YgATpDbwHRulSWUgxsNGliV17jf7AuAu0C21U/C5uwTemAxMcERLpaEvID9/sM3X2m3so+yY6veoyZ7eOqi5weXkAhKrVAvLFM3hqPrJmJ6INsn979jA9WkKB6U=</EncryptedData>
    </connectionStrings>
  
  
    <startup>
      <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.7.2" />
    </startup>
  </configuration>
  ```

  

#### Entschlüsselte Konfiguration

Der ```connectionStrings``` Knoten wurde hier wieder entschlüsselt - und sein Inhalt wieder in Klartext hergestellt. Wie zu sehen ist, wird der Inhalt des Knotens nicht notwendigerweise 1:1 wiederhergestellt... Allerdings ist die inhaltliche Bedeutung identisch.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <configProtectedData>
    <providers>
      <add name="SymmetricProtectedConfigurationProvider"
        type="de.Aargenveldt.Auxiliary.Configuration.Encoding.ConfigurationProviders.SymmetricProtectedConfigurationProvider, SymmetricProtectedConfigurationProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=63b973b832c470c2, processorArchitecture=MSIL"
        seed="AKeySeed"
        salt="QUtleVNhbHQ=" />
    </providers>
  </configProtectedData>


  <connectionStrings>
    <clear />
    <add name="foo" connectionString="This is some foo plaintext connectionstring"
      providerName="" />
    <add name="bar" connectionString="This is some bar plaintext connectionstring"
      providerName="" />
  </connectionStrings>


  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.7.2" />
  </startup>
</configuration>
```

## Projektaufbau

Das Gesamtprojekt besteht aus zwei Unterprojekten:

1. SymmetricProtectedConfigurationProvider

   Eine Klassenbibliothek, in der der ```SymmetricProtectedConfigurationProvider``` (Beschreibung siehe oben) implementiert wird.

   Die Klassenbibliothek ist mit einem starken Namen (Strongname) versehen. Das Zertifikat ist eigenständig [vom Visual Studio] generiert worden.

   Die Signatur ist eine Anforderung des Laufzeitsystems an spezifische Implementierungen von ProtectedConfigurationProvider.

2. AppConfigEncodingSpike

   Eine Konsolenanwendung, die die Nutzung von ```SymmetricProtectedConfigurationProvider``` als spezifische implementierung eines ProtectedConfigurationProvider demonstriert. Die Anwendung ist in der Lage, die eigene Konfiguration (```app.config```) zu ver- und entschlüsseln - und gibt ansonsten die Verbindungszeichenfolgen aus der Konfiguration - sowie den Verschlüsselungsstatus der ```ConnectionStrings```Sektion in der ```app.config``` - auf der Konsole aus.

   Da die Ver- und Entschlüsselung zur Laufzeit transparent erfolgt, hat die Demoanwendung **immer** Zugriff auf den Klartext der Verbindungszeichenfolge - unabhängig vom Verschlüsselungszustand.

Die Projekte verwenden das .NET Framework 4.7.2. Die Solution wurde ursprgl. mit Visual Studio 2022 (Community Edition) erstellt.

## Nutzung der Anwendung

Die Applikation ```AppConfigEncodingSpike.exe``` ist eine Konsolenanwendung und dient der Demonstration von

* Verschlüsselung der eigenen ```app.config```

* Entschlüsselung der eigenen ```app.config```

* Anzeige von

  * Verschlüsselungsstatus der ```ConnectionStrings```Sektion in der eigenen ```app.config```

  * Klartext der Verbindungszeichenfolgen

    Da die Ver- und Entschlüsselung zur Laufzeit transparent erfolgt, hat die Demoanwendung **immer** Zugriff auf den Klartext der Verbindungszeichenfolge - unabhängig vom Verschlüsselungszustand.



Die Anwendung kann über einen Parameter beim Aufruf in der Konsole gesteuert werden:

| Parameter | Beschreibung                                                 |
| --------- | ------------------------------------------------------------ |
| show      | Zeigt den Verschlüsselungsstatus der ```ConnectionStrings``` Sektion in der ```app.config``` der Anwendung an - sowie den Klartext der Verbindungszeichenfolgen ("foo" und "bar"):<br /><br />    ```Section connectionStrings is protected (not locked).```<br />    ``` Section connectionStrings is connection strings section; Count=2```<br/>      ```1 --> "This is some foo plaintext connectionstring"```<br/>      ```2 --> "This is some bar plaintext connectionstring"```<br/><br/>```Press <ENTER> to exit...```<br /> |
| protect   | Verschlüsselt die ```ConnectionStrings``` Sektion in der eigenen ```app.config```. Ist der Abschnitt bereits verschlüsselt, dann wird das erkannt und entsprechend angezeigt.<br />Dies benötigt ggf. administrative Berechtigungen, da eine Datei im Anwendungsverzeichnis geschrieben wird. |
| unprotect | Entschlüsselt die ```ConnectionStrings``` Sektion in der eigenen ```app.config```. Ist der Abschnitt bereits entschlüsselt, dann wird das erkannt und entsprechend angezeigt.<br />Dies benötigt ggf. administrative Berechtigungen, da eine Datei im Anwendungsverzeichnis geschrieben wird. |

Durch Betätigung der ```Enter``` (```Return```) Taste wird die Anwendung beendet.

Bei Aufruf ohne Parameter - oder mit einem unbekannten Parameter - erfolgt die Ausgabe eines Hilfetexts auf der Konsole.