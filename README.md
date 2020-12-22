## ЛР5

В репозитории остались файлы, которых коснулся рефакторинг.
 
[Видео](https://youtu.be/IznD6gPH5i8), демонстрирующее работу программы.

## Кратко о новом

  Многие методы стали асинхронными, с использованием стиля **TAP**, например: методы по работе с файлами(архивирование, перенос, удаление etc), логгирование (пример ниже), парсинг результата выполнения хранимой процедуры и тп. 
  
  Пример:

```C#

private async Task RecordEntryAsync(string fileEvent, string filePath)
{
  await Task.Run(()=>
  {
    using (StreamWriter writer = GetStreamWriter(logPath, 10_000))
    {
      writer.WriteLine(String.Format("{0} file {1} was {2}\n",
        DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss"), filePath, fileEvent));
      writer.Flush();
    }
  });
}

```

На каждую итерацию службы (Как **FileManager**, так и **DataManager**) запускается отдельный поток из ThreadPool.

```C#

ThreadPool.QueueUserWorkItem(Added, path);

```

## Касание асинхронности

[***DataAccess***](https://github.com/Lirosk/CS2_5/tree/main/DataAccess) — достает данные из бд, исполнив хранимую процедуру.
  
[***DataManager***](https://github.com/Lirosk/CS2_5/tree/main/DataManager) — мониторит наличие новых указаний, обязует *DataAccess* достать данные, *ServiceLayer* — обернуть да поместить куда надо.

[***FileManger***](https://github.com/Lirosk/CS2_5/tree/main/FileManager) — мониторит посылки от вышестоящего товарища.

[***ServiceLayer***](https://github.com/Lirosk/CS2_5/tree/main/ServiceLayer) — помещает таблицу в *.xml* файл, кладет рядом его *.xsd*.
