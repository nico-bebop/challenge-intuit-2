namespace ConsultasPlan.Exceptions;

/// <summary>Excepciones con semántica de dominio; el middleware global las traduce a HTTP.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

/// <summary>El recurso solicitado no existe → 404.</summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Se violó una regla de negocio o la solicitud es inválida → 400.</summary>
public class BusinessException : DomainException
{
    public BusinessException(string message) : base(message) { }
}

/// <summary>La operación choca con el estado actual de los datos (ej. DNI duplicado) → 409.</summary>
public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}
